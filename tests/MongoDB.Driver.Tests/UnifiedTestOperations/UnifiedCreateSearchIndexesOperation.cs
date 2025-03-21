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
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MongoDB.Bson;
using MongoDB.Driver.Core.Misc;

namespace MongoDB.Driver.Tests.UnifiedTestOperations
{
    public sealed class UnifiedCreateSearchIndexesOperation : IUnifiedEntityTestOperation
    {
        private readonly IMongoCollection<BsonDocument> _collection;
        private readonly CreateSearchIndexModel[] _createSearchIndexModels;

        public UnifiedCreateSearchIndexesOperation(
            IMongoCollection<BsonDocument> collection,
            CreateSearchIndexModel[] createSearchIndexModels)
        {
            _collection = Ensure.IsNotNull(collection, nameof(collection));
            _createSearchIndexModels = createSearchIndexModels;
        }

        public OperationResult Execute(CancellationToken cancellationToken)
        {
            try
            {
                var result = _collection.SearchIndexes.CreateMany(_createSearchIndexModels, cancellationToken: cancellationToken);

                return OperationResult.FromResult(BsonArray.Create(result));
            }
            catch (Exception exception)
            {
                return OperationResult.FromException(exception);
            }
        }

        public async Task<OperationResult> ExecuteAsync(CancellationToken cancellationToken)
        {
            try
            {
                var result = await _collection.SearchIndexes.CreateManyAsync(_createSearchIndexModels, cancellationToken: cancellationToken);

                return OperationResult.FromResult(BsonArray.Create(result));
            }
            catch (Exception exception)
            {
                return OperationResult.FromException(exception);
            }
        }
    }

    public sealed class UnifiedCreateSearchIndexesOperationBuilder
    {
        private readonly UnifiedEntityMap _entityMap;

        public UnifiedCreateSearchIndexesOperationBuilder(UnifiedEntityMap entityMap)
        {
            _entityMap = entityMap;
        }

        public UnifiedCreateSearchIndexesOperation Build(string targetCollectionId, BsonDocument arguments)
        {
            if (arguments.ElementCount != 1 || arguments.First().Name != "models")
            {
                throw new FormatException($"Expected single CreateSearchIndexOperation argument 'model'.");
            }

            var collection = _entityMap.Collections[targetCollectionId];
            var models = arguments["models"].AsBsonArray;
            var parsedModels = new List<CreateSearchIndexModel>();

            foreach (var model in models)
            {
                BsonDocument definition = null;
                string name = null;
                SearchIndexType? type = null;

                foreach (var argument in model.AsBsonDocument)
                {
                    switch (argument.Name)
                    {
                        case "name":
                            name = argument.Value.AsString;
                            break;
                        case "type":
                            type = argument.Value.AsString switch
                            {
                                "search" => SearchIndexType.Search,
                                "vectorSearch" => SearchIndexType.VectorSearch,
                                _ => throw new FormatException($"Unexpected search index type '{argument.Value}'.")
                            };
                            break;
                        case "definition":
                            definition = argument.Value.AsBsonDocument;
                            break;
                        default:
                            throw new FormatException($"Invalid CreateSearchIndexOperation model argument name: '{argument.Name}'.");
                    }
                }

                parsedModels.Add(new(name, type, definition));
            }


            return new(collection, parsedModels.ToArray());
        }
    }
}
