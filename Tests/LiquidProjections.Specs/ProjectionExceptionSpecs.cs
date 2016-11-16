using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;

using Chill;

using FluentAssertions;

using Xunit;

namespace LiquidProjections.Specs
{
    namespace ProjectionExceptionSpecs
    {
        public abstract class Given_the_exception_with_serializable_events_and_headers : GivenSubject<ProjectionException>
        {
            protected Given_the_exception_with_serializable_events_and_headers()
            {
                Given(() =>
                {
                    UseThe(new EventEnvelope
                    {
                        Body = new SomeEvent { X = 42 },
                        Headers = new Dictionary<string, object>
                        {
                            ["SomeEventHeader"] = "SomeEventHeaderValue1"
                        }
                    });

                    UseThe(new Transaction
                    {
                        Id = "Transaction1",
                        Checkpoint = 3,
                        Events = new[] { The<EventEnvelope>() },
                        Headers = new Dictionary<string, object>
                        {
                            ["SomeTransactionHeader"] = "SomeTransactionHeaderValue1"
                        },
                        StreamId = "Stream1",
                        TimeStampUtc = new DateTime(2016, 11, 14, 8, 2, 14, DateTimeKind.Utc)
                    });

                    WithSubject(_ => new ProjectionException("SomeMessage", new InvalidOperationException())
                    {
                        CurrentEvent = The<EventEnvelope>(),
                        Projector = "SomeProjector",
                        ChildProjector = "SomeChildProjector",
                        TransactionId = The<Transaction>().Id
                    });

                    Subject.SetTransactionBatch(new[] { The<Transaction>() });
                });
            }
        }

        public class When_serialized_and_deserialized : Given_the_exception_with_serializable_events_and_headers
        {
            private ProjectionException result;

            public When_serialized_and_deserialized()
            {
                Given(() =>
                {
                    UseThe(new BinaryFormatter());
                });

                When(() =>
                {
                    byte[] serializedException;

                    using (var stream = new MemoryStream())
                    {
                        The<BinaryFormatter>().Serialize(stream, Subject);
                        serializedException = stream.ToArray();
                    }

                    using (var stream = new MemoryStream(serializedException))
                    {
                        result = (ProjectionException)The<BinaryFormatter>().Deserialize(stream);
                    }
                });
            }

            [Fact]
            public void It_should_have_the_same_current_event()
            {
                result.CurrentEvent.ShouldBeEquivalentTo(Subject.CurrentEvent);
            }

            [Fact]
            public void It_should_have_the_same_transaction_id()
            {
                result.TransactionId.Should().Be(Subject.TransactionId);
            }

            [Fact]
            public void It_should_have_the_same_projector()
            {
                result.Projector.Should().Be(Subject.Projector);
            }

            [Fact]
            public void It_should_have_the_same_child_projector()
            {
                result.ChildProjector.Should().Be(Subject.ChildProjector);
            }

            [Fact]
            public void It_should_have_the_same_transaction_batch()
            {
                result.TransactionBatch.ShouldBeEquivalentTo(Subject.TransactionBatch);
            }

            [Fact]
            public void It_should_have_the_same_inner_exception()
            {
                result.InnerException.Should().BeOfType<InvalidOperationException>();
            }

            [Fact]
            public void It_should_have_the_same_message()
            {
                result.Message.Should().Be(Subject.Message);
            }
        }

        [Serializable]
        internal class SomeEvent
        {
            public int X { get; set; }
        }
    }
}
