﻿/* Copyright 2010-present MongoDB Inc.
*
* Licensed under the Apache License, Version 2.0 (the "License");
* you may not use this file except in compliance with the License.
* You may obtain a copy of the License at
*
* http://www.apache.org/licenses/LICENSE-2.0
*
* Unless required by applicable law or agreed to in writing, software
* distributed under the License is distributed on an "AS IS" BASIS,
* WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
* See the License for the specific language governing permissions and
* limitations under the License.
*/

using System;
using System.Collections;
using System.Collections.Generic;
#if NET6_0_OR_GREATER
using System.Collections.Immutable;
#endif
using System.Collections.ObjectModel;
using System.Dynamic;
using System.Linq;
using System.Reflection;
using MongoDB.Bson.Serialization.Serializers;

namespace MongoDB.Bson.Serialization
{
    /// <summary>
    /// Provides serializers for collections.
    /// </summary>
    public class CollectionsSerializationProvider : BsonSerializationProviderBase
    {
        private static readonly Dictionary<Type, Type> __serializerTypes;

        static CollectionsSerializationProvider()
        {
            __serializerTypes = new Dictionary<Type, Type>
            {
                { typeof(BitArray), typeof(BitArraySerializer) },
                { typeof(ExpandoObject), typeof(ExpandoObjectSerializer) },
                { typeof(Queue), typeof(QueueSerializer) },
                { typeof(Stack), typeof(StackSerializer) },
                { typeof(Queue<>), typeof(QueueSerializer<>) },
                { typeof(ReadOnlyCollection<>), typeof(ReadOnlyCollectionSerializer<>) },
                { typeof(Stack<>), typeof(StackSerializer<>) },
                { typeof(Memory<>), typeof(MemorySerializer<>) },
                { typeof(ReadOnlyMemory<>), typeof(ReadonlyMemorySerializer<>) },
#if NET6_0_OR_GREATER
                { typeof(ImmutableArray<>), typeof(ImmutableArraySerializer<>) },
                { typeof(ImmutableList<>), typeof(ImmutableListSerializer<>) },
                { typeof(ImmutableHashSet<>), typeof(ImmutableHashSetSerializer<>) },
                { typeof(ImmutableSortedSet<>), typeof(ImmutableSortedSetSerializer<>) },
                { typeof(ImmutableDictionary<,>), typeof(ImmutableDictionarySerializer<,>) },
                { typeof(ImmutableSortedDictionary<,>), typeof(ImmutableSortedDictionarySerializer<,>) },
                { typeof(ImmutableQueue<>), typeof(ImmutableQueueSerializer<>) },
                { typeof(ImmutableStack<>), typeof(ImmutableStackSerializer<>) }
#endif
            };
        }

        private static bool IsOrIsChildOf(Type type, Type parent)
        {
            return type == parent || (type != null) && (type != typeof(object) && IsOrIsChildOf(type.GetTypeInfo().BaseType, parent));
        }

        /// <inheritdoc/>
        public override IBsonSerializer GetSerializer(Type type, IBsonSerializerRegistry serializerRegistry)
        {
            if (type == null)
            {
                throw new ArgumentNullException("type");
            }
            var typeInfo = type.GetTypeInfo();
            if (typeInfo.IsGenericType && typeInfo.ContainsGenericParameters)
            {
                var message = string.Format("Generic type {0} has unassigned type parameters.", BsonUtils.GetFriendlyTypeName(type));
                throw new ArgumentException(message, "type");
            }

            Type serializerType;
            if (__serializerTypes.TryGetValue(type, out serializerType))
            {
                return CreateSerializer(serializerType, serializerRegistry);
            }

            if (typeInfo.IsGenericType && !typeInfo.ContainsGenericParameters)
            {
                var genericTypeDefinition = type.GetGenericTypeDefinition();

                Type serializerTypeDefinition;
                if (__serializerTypes.TryGetValue(genericTypeDefinition, out serializerTypeDefinition))
                {
                    return CreateGenericSerializer(serializerTypeDefinition, typeInfo.GetGenericArguments(), serializerRegistry);
                }

                if (genericTypeDefinition == typeof(IOrderedEnumerable<>))
                {
                    var itemType = type.GetGenericArguments()[0];
                    var itemSerializer = serializerRegistry.GetSerializer(itemType);
                    var thenByExceptionMessage = "ThenBy or ThenByDescending are not supported here.";
                    return IOrderedEnumerableSerializer.Create(itemSerializer, thenByExceptionMessage);
                }
            }

            if (type.IsArray)
            {
                var elementType = type.GetElementType();
                switch (type.GetArrayRank())
                {
                    case 1:
                        var arraySerializerDefinition = typeof(ArraySerializer<>);
                        return CreateGenericSerializer(arraySerializerDefinition, new[] { elementType }, serializerRegistry);
                    case 2:
                        var twoDimensionalArraySerializerDefinition = typeof(TwoDimensionalArraySerializer<>);
                        return CreateGenericSerializer(twoDimensionalArraySerializerDefinition, new[] { elementType }, serializerRegistry);
                    case 3:
                        var threeDimensionalArraySerializerDefinition = typeof(ThreeDimensionalArraySerializer<>);
                        return CreateGenericSerializer(threeDimensionalArraySerializerDefinition, new[] { elementType }, serializerRegistry);
                    default:
                        var message = string.Format("No serializer found for array for rank {0}.", type.GetArrayRank());
                        throw new BsonSerializationException(message);
                }
            }

            var readOnlyDictionarySerializer = GetReadOnlyDictionarySerializer(type, serializerRegistry);
            if (readOnlyDictionarySerializer != null)
            {
                return readOnlyDictionarySerializer;
            }

            return GetCollectionSerializer(type, serializerRegistry);
        }

        private IBsonSerializer GetCollectionSerializer(Type type, IBsonSerializerRegistry serializerRegistry)
        {
            Type implementedGenericDictionaryInterface = null;
            Type implementedGenericEnumerableInterface = null;
            Type implementedGenericSetInterface = null;
            Type implementedDictionaryInterface = null;
            Type implementedEnumerableInterface = null;

            var implementedInterfaces = new List<Type>(type.GetTypeInfo().GetInterfaces());
            var typeInfo = type.GetTypeInfo();
            if (typeInfo.IsInterface)
            {
                implementedInterfaces.Add(type);
            }

            foreach (var implementedInterface in implementedInterfaces)
            {
                var implementedInterfaceTypeInfo = implementedInterface.GetTypeInfo();
                if (implementedInterfaceTypeInfo.IsGenericType)
                {
                    var genericInterfaceDefinition = implementedInterface.GetGenericTypeDefinition();
                    if (genericInterfaceDefinition == typeof(IDictionary<,>))
                    {
                        implementedGenericDictionaryInterface = implementedInterface;
                    }
                    if (genericInterfaceDefinition == typeof(IEnumerable<>))
                    {
                        implementedGenericEnumerableInterface = implementedInterface;
                    }
                    if (genericInterfaceDefinition == typeof(ISet<>))
                    {
                        implementedGenericSetInterface = implementedInterface;
                    }
                }
                else
                {
                    if (implementedInterface == typeof(IDictionary))
                    {
                        implementedDictionaryInterface = implementedInterface;
                    }
                    if (implementedInterface == typeof(IEnumerable))
                    {
                        implementedEnumerableInterface = implementedInterface;
                    }
                }
            }

            // the order of the tests is important
            if (implementedGenericDictionaryInterface != null)
            {
                var keyType = implementedGenericDictionaryInterface.GetTypeInfo().GetGenericArguments()[0];
                var valueType = implementedGenericDictionaryInterface.GetTypeInfo().GetGenericArguments()[1];
                if (typeInfo.IsInterface)
                {
                    var dictionaryDefinition = typeof(Dictionary<,>);
                    var dictionaryType = dictionaryDefinition.MakeGenericType(keyType, valueType);
                    var serializerDefinition = typeof(ImpliedImplementationInterfaceSerializer<,>);
                    return CreateGenericSerializer(serializerDefinition, new[] { type, dictionaryType }, serializerRegistry);
                }
                else
                {
                    var serializerDefinition = typeof(DictionaryInterfaceImplementerSerializer<,,>);
                    return CreateGenericSerializer(serializerDefinition, new[] { type, keyType, valueType }, serializerRegistry);
                }
            }
            else if (implementedDictionaryInterface != null)
            {
                if (typeInfo.IsInterface)
                {
                    var dictionaryType = typeof(Hashtable);
                    var serializerDefinition = typeof(ImpliedImplementationInterfaceSerializer<,>);
                    return CreateGenericSerializer(serializerDefinition, new[] { type, dictionaryType }, serializerRegistry);
                }
                else
                {
                    var serializerDefinition = typeof(DictionaryInterfaceImplementerSerializer<>);
                    return CreateGenericSerializer(serializerDefinition, new[] { type }, serializerRegistry);
                }
            }
            else if (implementedGenericSetInterface != null)
            {
                var itemType = implementedGenericSetInterface.GetTypeInfo().GetGenericArguments()[0];

                if (typeInfo.IsInterface)
                {
                    var serializerDefinition = typeof(IEnumerableDeserializingAsCollectionSerializer<,,>);
                    var collectionType = typeof(HashSet<>).MakeGenericType(itemType);
                    return CreateGenericSerializer(serializerDefinition, new[] { type, itemType, collectionType }, serializerRegistry);
                }
                else
                {
                    var serializerDefinition = typeof(EnumerableInterfaceImplementerSerializer<,>);
                    return CreateGenericSerializer(serializerDefinition, new[] { type, itemType }, serializerRegistry);
                }
            }
            else if (implementedGenericEnumerableInterface != null)
            {
                var itemType = implementedGenericEnumerableInterface.GetTypeInfo().GetGenericArguments()[0];

                var readOnlyCollectionType = typeof(ReadOnlyCollection<>).MakeGenericType(itemType);
                if (type == readOnlyCollectionType)
                {
                    var serializerDefinition = typeof(ReadOnlyCollectionSerializer<>);
                    return CreateGenericSerializer(serializerDefinition, new[] { itemType }, serializerRegistry);
                }
                else if (readOnlyCollectionType.GetTypeInfo().IsAssignableFrom(type))
                {
                    var serializerDefinition = typeof(ReadOnlyCollectionSubclassSerializer<,>);
                    return CreateGenericSerializer(serializerDefinition, new[] { type, itemType }, serializerRegistry);
                }
                else if (typeInfo.IsInterface)
                {
                    var listType = typeof(List<>).MakeGenericType(itemType);
                    if (typeInfo.IsAssignableFrom(listType))
                    {
                        var serializerDefinition = typeof(IEnumerableDeserializingAsCollectionSerializer<,,>);
                        var collectionType = typeof(List<>).MakeGenericType(itemType);
                        return CreateGenericSerializer(serializerDefinition, new[] { type, itemType, collectionType }, serializerRegistry);
                    }
                }

                var enumerableSerializerDefinition = typeof(EnumerableInterfaceImplementerSerializer<,>);
                return CreateGenericSerializer(enumerableSerializerDefinition, new[] { type, itemType }, serializerRegistry);
            }
            else if (implementedEnumerableInterface != null)
            {
                if (typeInfo.IsInterface)
                {
                    var listType = typeof(ArrayList);
                    var serializerDefinition = typeof(ImpliedImplementationInterfaceSerializer<,>);
                    return CreateGenericSerializer(serializerDefinition, new[] { type, listType }, serializerRegistry);
                }
                else
                {
                    var serializerDefinition = typeof(EnumerableInterfaceImplementerSerializer<>);
                    return CreateGenericSerializer(serializerDefinition, new[] { type }, serializerRegistry);
                }
            }

            return null;
        }

        private List<Type> GetImplementedInterfaces(Type type)
        {
            var typeInfo = type.GetTypeInfo();
            return typeInfo.IsInterface
                ? typeInfo.GetInterfaces().Concat(new Type[] { type }).ToList()
                : typeInfo.GetInterfaces().ToList();
        }

        private IBsonSerializer GetReadOnlyDictionarySerializer(Type type, IBsonSerializerRegistry serializerRegistry)
        {
            var typeInfo = type.GetTypeInfo();
            if (!typeInfo.IsGenericType
                || typeInfo.IsGenericTypeDefinition
                || typeInfo.GetGenericArguments().Length != 2)
            {
                return null;
            }

            var keyType = typeInfo.GetGenericArguments()[0];
            var valueType = typeInfo.GetGenericArguments()[1];
            var typeIsIReadOnlyDictionary =
                type == typeof(IReadOnlyDictionary<,>).MakeGenericType(keyType, valueType);
            var typeIsOrIsChildOfReadOnlyDictionary =
                IsOrIsChildOf(type, typeof(ReadOnlyDictionary<,>).MakeGenericType(keyType, valueType));

            var implementedInterfaces = GetImplementedInterfaces(type);
            var genericImplementedInterfaces = implementedInterfaces.Where(ii => ii.GetTypeInfo().IsGenericType);
            var genericImplementedInterfaceDefinitions =
                genericImplementedInterfaces.Select(i => i.GetGenericTypeDefinition()).ToArray();
            var implementsGenericReadOnlyDictionaryInterface =
                genericImplementedInterfaceDefinitions.Contains(typeof(IReadOnlyDictionary<,>));
            var implementsGenericDictionaryInterface =
                genericImplementedInterfaceDefinitions.Contains(typeof(IDictionary<,>));

            if (typeIsIReadOnlyDictionary)
            {
                return CreateGenericSerializer(
                    serializerTypeDefinition: typeof(ImpliedImplementationInterfaceSerializer<,>),
                    typeArguments: new[] { type, typeof(ReadOnlyDictionary<,>).MakeGenericType(keyType, valueType) },
                    serializerRegistry: serializerRegistry);
            }

            if (typeIsOrIsChildOfReadOnlyDictionary
                || (!typeInfo.IsInterface
                    && implementsGenericReadOnlyDictionaryInterface
                    && !implementsGenericDictionaryInterface))
            {
                return CreateGenericSerializer(
                    serializerTypeDefinition: typeof(ReadOnlyDictionaryInterfaceImplementerSerializer<,,>),
                    typeArguments: new[] { type, keyType, valueType },
                    serializerRegistry: serializerRegistry);
            }

            return null;

        }

    }
}
