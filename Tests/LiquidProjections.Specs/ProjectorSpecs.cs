using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Chill;
using FluentAssertions;
using LiquidProjections.Abstractions;
using LiquidProjections.Testing;
using Xunit;

namespace LiquidProjections.Specs
{
    namespace ProjectorSpecs
    {
        public class Given_a_projector_with_an_in_memory_event_source : GivenSubject<Projector>
        {
            protected EventMapBuilder<ProjectionContext> Events;

            public Given_a_projector_with_an_in_memory_event_source()
            {
                Given(() =>
                {
                    UseThe(new MemoryEventSource());
                    Events = new EventMapBuilder<ProjectionContext>();

                    // UseThe cannot be used here due to a bug in Chill.
                    Container.Set<IEventMapBuilder<ProjectionContext>>(Events, string.Empty);
                });
            }

            protected void StartProjecting()
            {
                The<MemoryEventSource>().Subscribe(110, new Subscriber
                {
                    HandleTransactions = async (transactions, info) => await Subject.Handle(transactions)
                }, "");
            }
        }

        public class When_an_event_requires_a_custom_action : Given_a_projector_with_an_in_memory_event_source
        {
            private string discontinuedCategory;
            private ProjectionContext context;

            public When_an_event_requires_a_custom_action()
            {
                Given(() =>
                {
                    Events.Map<CategoryDiscontinuedEvent>().As((@event, context) =>
                    {
                        discontinuedCategory = @event.Category;
                        this.context = context;
                    });

                    StartProjecting();
                });

                When(async () =>
                {
                    await The<MemoryEventSource>().Write(new Transaction
                    {
                        Checkpoint = 111,
                        Id = "MyTransactionId",
                        StreamId = "MyStreamId",
                        TimeStampUtc = 10.April(1979).At(13, 14, 15),
                        Headers = new Dictionary<string, object>
                        {
                            ["My custom header"] = "My custom header value"
                        },
                        Events = new List<EventEnvelope>
                        {
                            new EventEnvelope
                            {
                                Body = new CategoryDiscontinuedEvent
                                {
                                    Category = "Hybrid"
                                },
                                Headers = new Dictionary<string, object>
                                {
                                    ["Some event header"] = "Some event header value"
                                }
                            }
                        }
                    });
                });
            }

            [Fact]
            public void Then_it_should_have_executed_the_custom_action()
            {
                discontinuedCategory.Should().Be("Hybrid");
            }

            [Fact]
            public void Then_it_should_have_created_the_context()
            {
                context.ShouldBeEquivalentTo(new ProjectionContext
                {
                    Checkpoint = 111,
                    TransactionId = "MyTransactionId",
                    StreamId = "MyStreamId",
                    TimeStampUtc = 10.April(1979).At(13, 14, 15),
                    TransactionHeaders = new Dictionary<string, object>
                    {
                        ["My custom header"] = "My custom header value"
                    },
                    EventHeaders = new Dictionary<string, object>
                    {
                        ["Some event header"] = "Some event header value"
                    }
                });
            }
        }

        public class When_an_event_is_not_mapped_at_all : Given_a_projector_with_an_in_memory_event_source
        {
            public When_an_event_is_not_mapped_at_all()
            {
                Given(() => StartProjecting());

                When(async () =>
                {
                    await The<MemoryEventSource>().Write(new ProductAddedToCatalogEvent());
                }, deferredExecution: true);
            }

            [Fact]
            public void Then_it_should_not_fail()
            {
                WhenAction.ShouldNotThrow();
            }
        }

        public class When_an_event_has_a_header : Given_a_projector_with_an_in_memory_event_source
        {
            public When_an_event_has_a_header()
            {
                Given(() =>
                {
                    // Required due to a bug in Chill.
                    UseThe(new ProductCatalogEntry());

                    Events.Map<ProductAddedToCatalogEvent>().As((anEvent, context) =>
                    {
                        The<ProductCatalogEntry>().AddedBy = (string)context.EventHeaders["UserName"];
                    });

                    StartProjecting();
                });

                When(async () =>
                {
                    await The<MemoryEventSource>().WriteWithHeaders(
                        new ProductAddedToCatalogEvent(),
                        new Dictionary<string, object>
                        {
                            ["UserName"] = "Pavel"
                        });
                });
            }

            [Fact]
            public void Then_it_should_use_the_header()
            {
                The<ProductCatalogEntry>().AddedBy.Should().Be("Pavel");
            }
        }

        public class When_a_transaction_has_a_header : Given_a_projector_with_an_in_memory_event_source
        {
            public When_a_transaction_has_a_header()
            {
                Given(() =>
                {
                    // Required due to a bug in Chill.
                    UseThe(new ProductCatalogEntry());

                    Events.Map<ProductAddedToCatalogEvent>().As((_, context) =>
                    {
                        The<ProductCatalogEntry>().AddedBy = (string)context.TransactionHeaders["UserName"];
                    });

                    StartProjecting();
                });

                When(async () =>
                {
                    await The<MemoryEventSource>().Write(new Transaction
                    {
                        Events = new[]
                        {
                            new EventEnvelope
                            {
                                Body = new ProductAddedToCatalogEvent()
                            }
                        },
                        Headers = new Dictionary<string, object>
                        {
                            ["UserName"] = "Pavel"
                        }
                    });
                });
            }

            [Fact]
            public void Then_it_should_use_the_header()
            {
                The<ProductCatalogEntry>().AddedBy.Should().Be("Pavel");
            }
        }

        public class When_event_handling_fails : Given_a_projector_with_an_in_memory_event_source
        {
            public When_event_handling_fails()
            {
                Given(() =>
                {
                    UseThe(new InvalidOperationException());

                    Events.Map<CategoryDiscontinuedEvent>().As(_ =>
                    {
                        throw The<InvalidOperationException>();
                    });

                    UseThe(new Transaction
                    {
                        Events = new[]
                        {
                            UseThe(new EventEnvelope
                            {
                                Body = The<CategoryDiscontinuedEvent>()
                            })
                        }
                    });

                    StartProjecting();
                });

                When(() => The<MemoryEventSource>().Write(The<Transaction>()), deferredExecution: true);
            }

            [Fact]
            public void Then_it_should_wrap_the_exception_into_a_projection_exception()
            {
                WhenAction.ShouldThrow<ProjectionException>()
                    .Which.InnerException.Should().BeSameAs(The<InvalidOperationException>());
            }

            [Fact]
            public void Then_it_should_include_the_current_event_into_the_projection_exception()
            {
                WhenAction.ShouldThrow<ProjectionException>().Which.CurrentEvent.Should().Be(The<EventEnvelope>());
            }

            [Fact]
            public void Then_it_should_include_the_current_transaction_batch_into_the_projection_exception()
            {
                WhenAction.ShouldThrow<ProjectionException>().Which.TransactionBatch.Should().BeEquivalentTo(The<Transaction>());
            }
        }

        public class When_event_handling_fails_with_a_custom_exception_policy :
            Given_a_projector_with_an_in_memory_event_source
        {
            private bool succeeded;
            private int numerOfFailedAttempts;
            private const int NumberOfTimesToFail = 3;

            public When_event_handling_fails_with_a_custom_exception_policy()
            {
                Given(() =>
                {
                    UseThe(new InvalidOperationException());

                    Events.Map<CategoryDiscontinuedEvent>().As((@event, context) =>
                    {
                        if (numerOfFailedAttempts < NumberOfTimesToFail)
                        {
                            throw The<InvalidOperationException>();
                        }

                        succeeded = true;
                    });

                    StartProjecting();

                    Subject.ShouldRetry = (exception, attempts) =>
                    {
                        return Task.Run(() =>
                        {
                            numerOfFailedAttempts = attempts;
                            if (attempts <= NumberOfTimesToFail)
                            {
                                return true;
                            }

                            return false;
                        });
                    };

                    UseThe(new Transaction
                    {
                        Events = new[]
                        {
                            UseThe(new EventEnvelope
                            {
                                Body = new CategoryDiscontinuedEvent()
                            })
                        }
                    });
                });

                When(() => The<MemoryEventSource>().Write(The<Transaction>()));
            }

            [Fact]
            public void Then_it_should_try_again()
            {
                numerOfFailedAttempts.Should().Be(NumberOfTimesToFail);
            }

            [Fact]
            public void Then_it_should_succeed()
            {
                succeeded.Should().BeTrue();
            }
        }

        public class ProductCatalogEntry
        {
            public string Id { get; set; }
            public string Category { get; set; }
            public string AddedBy { get; set; }
        }

        public class ProductAddedToCatalogEvent
        {
            public string ProductKey { get; set; }
            public string Category { get; set; }
        }

        public class CategoryDiscontinuedEvent
        {
            public string Category { get; set; }
        }
    }
}
