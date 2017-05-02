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

        internal class SomeEvent
        {
            public int X { get; set; }
        }
    }
}
