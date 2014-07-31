﻿/* Copyright 2013-2014 MongoDB Inc.
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
using System.Net;
using System.Text;
using System.Threading.Tasks;
using FluentAssertions;
using MongoDB.Driver.Core.Clusters;
using MongoDB.Driver.Core.Servers;
using NUnit.Framework;

namespace MongoDB.Driver.Core.Tests.Clusters
{
    [TestFixture]
    public class ClusterDescriptionTests
    {
        #region static
        // static fields
        private static readonly ClusterId __clusterId;
        private static readonly DnsEndPoint __endPoint1;
        private static readonly DnsEndPoint __endPoint2;
        private static readonly ReplicaSetConfig __replicaSetConfig;
        private static readonly ServerDescription __serverDescription1;
        private static readonly ServerDescription __serverDescription2;
        private static readonly ServerId __serverId1;
        private static readonly ServerId __serverId2;

        // static constructor
        static ClusterDescriptionTests()
        {
            __clusterId = new ClusterId();

            __endPoint1 = new DnsEndPoint("localhost", 27017);
            __endPoint2 = new DnsEndPoint("localhost", 27018);
            __serverId1 = new ServerId(__clusterId, __endPoint1);
            __serverId2 = new ServerId(__clusterId, __endPoint2);
            __serverDescription1 = new ServerDescription(__serverId1, __endPoint1);
            __serverDescription2 = new ServerDescription(__serverId2, __endPoint2);
            __replicaSetConfig = new ReplicaSetConfig(
                new[] { __endPoint1, __endPoint2 },
                "name",
                __endPoint1,
                1);
        }
        #endregion

        // static member tests
        [Test]
        public void CreateDisposed_should_return_disposed_description()
        {
            var subject = ClusterDescription.CreateDisposed(__clusterId, ClusterType.Standalone);
            subject.ClusterId.Should().Be(__clusterId);
            subject.ReplicaSetConfig.Should().BeNull();
            subject.Revision.Should().Be(0);
            subject.Servers.Should().BeEmpty();
            subject.State.Should().Be(ClusterState.Disposed);
            subject.Type.Should().Be(ClusterType.Standalone);
        }

        [Test]
        public void CreateUnitialized_should_return_unitialized_description()
        {
            var subject = ClusterDescription.CreateUninitialized(__clusterId, ClusterType.Standalone);
            subject.ClusterId.Should().Be(__clusterId);
            subject.ReplicaSetConfig.Should().BeNull();
            subject.Revision.Should().Be(0);
            subject.Servers.Should().BeEmpty();
            subject.State.Should().Be(ClusterState.Uninitialized);
            subject.Type.Should().Be(ClusterType.Standalone);
        }

        // instance member tests
        [Test]
        public void Constructor_should_initialize_instance()
        {
            var subject = new ClusterDescription(
                __clusterId,
                ClusterType.ReplicaSet,
                ClusterState.Connected,
                new[] { __serverDescription1, __serverDescription2 },
                __replicaSetConfig,
                1);
            subject.ClusterId.Should().Be(__clusterId);
            subject.ReplicaSetConfig.Should().Be(__replicaSetConfig);
            subject.Revision.Should().Be(1);
            subject.Servers.Should().ContainInOrder(new[] { __serverDescription1, __serverDescription2 });
            subject.State.Should().Be(ClusterState.Connected);
            subject.Type.Should().Be(ClusterType.ReplicaSet);
        }

        private ClusterDescription CreateSubject(string notEqualField = null)
        {
            var clusterId = new ClusterId(1);
            var type = ClusterType.ReplicaSet;
            var state = ClusterState.Connected;
            var servers = new[] { __serverDescription1, __serverDescription2 };
            var replicaSetConfig = new ReplicaSetConfig(new[] { __endPoint1, __endPoint2 }, "name", __endPoint1, 1);
            var revision = 1;

            switch (notEqualField)
            {
                case "ClusterId": clusterId = new ClusterId(2); break;
                case "Type": type = ClusterType.Unknown ; break;
                case "State": state = ClusterState.PartiallyConnected ; break;
                case "Servers": servers = new[] { __serverDescription1 }; break;
                case "ReplicaSetConfig": replicaSetConfig = new ReplicaSetConfig(new[] { __endPoint1 }, "name", __endPoint1, 1); break;
                case "Revision":  revision = 2; break;
            }

            return new ClusterDescription(clusterId, type, state, servers, replicaSetConfig, revision);
        }

        [Test]
        public void Equals_should_ignore_revision()
        {
            var subject1 = CreateSubject();
            var subject2 = CreateSubject("Revision");
            subject1.Equals(subject2).Should().BeTrue();
            subject1.Equals((object)subject2).Should().BeTrue();
            subject1.GetHashCode().Should().Be(subject2.GetHashCode());
        }

        [TestCase("ClusterId")]
        [TestCase("ReplicaSetConfig")]
        [TestCase("Servers")]
        [TestCase("State")]
        [TestCase("Type")]
        public void Equals_should_return_false_if_any_field_is_not_equal(string notEqualField)
        {
            var subject1 = CreateSubject();
            var subject2 = CreateSubject(notEqualField);
            subject1.Equals(subject2).Should().BeFalse();
            subject1.Equals((object)subject2).Should().BeFalse();
            subject1.GetHashCode().Should().NotBe(subject2.GetHashCode());
        }

        [Test]
        public void Equals_should_return_true_if_all_fields_are_equal()
        {
            var subject1 = CreateSubject();
            var subject2 = CreateSubject();
            subject1.Equals(subject2).Should().BeTrue();
            subject1.Equals((object)subject2).Should().BeTrue();
            subject1.GetHashCode().Should().Be(subject2.GetHashCode());
        }

        [Test]
        public void ToString_should_return_string_representation()
        {
            var subject = new ClusterDescription(new ClusterId(1), ClusterType.Standalone, ClusterState.Connected, new[] { __serverDescription1 }, null, 1);
            var expected = string.Format("{{ ClusterId : 1, Type : Standalone, State : Connected, Servers : [{0}], ReplicaSetConfig : null, Revision : 1 }}",
                __serverDescription1);
            subject.ToString().Should().Be(expected);
        }

        [Test]
        public void WithRevision_should_return_new_instance_if_value_is_not_equal()
        {
            var subject1 = CreateSubject();
            var subject2 = subject1.WithRevision(subject1.Revision + 1);
            subject2.Should().NotBeSameAs(subject1);
            subject2.Should().Be(subject1);
        }

        [Test]
        public void WithRevision_should_return_same_instance_if_value_is_equal()
        {
            var subject1 = CreateSubject();
            var subject2 = subject1.WithRevision(subject1.Revision);
            subject2.Should().BeSameAs(subject1);
        }

        [Test]
        public void WithServerDescription_should_return_new_instance_if_value_is_not_equal()
        {
            var subject1 = CreateSubject();
            var oldServerDescription = subject1.Servers[0];
            var newServerDescription = oldServerDescription.WithHeartbeatInfo(
                oldServerDescription.AverageRoundTripTime.Add(TimeSpan.FromSeconds(1)),
                oldServerDescription.ReplicaSetConfig,
                oldServerDescription.Tags,
                oldServerDescription.Type,
                oldServerDescription.Version);
            var subject2 = subject1.WithServerDescription(newServerDescription);
            subject2.Should().NotBeSameAs(subject1);
            subject2.Should().NotBe(subject1);
        }

        [Test]
        public void WithServerDescription_should_return_same_instance_if_value_is_equal()
        {
            var subject1 = CreateSubject();
            var subject2 = subject1.WithServerDescription(subject1.Servers[0]);
            subject2.Should().BeSameAs(subject1);
        }

        [Test]
        public void WithType_should_return_new_instance_if_value_is_not_equal()
        {
            var subject1 = CreateSubject();
            var subject2 = subject1.WithType(ClusterType.Unknown);
            subject2.Should().NotBeSameAs(subject1);
            subject2.Should().NotBe(subject1);
        }

        [Test]
        public void WithType_should_return_same_instance_if_value_is_equal()
        {
            var subject1 = CreateSubject();
            var subject2 = subject1.WithType(subject1.Type);
            subject2.Should().BeSameAs(subject1);
        }
    }
}
