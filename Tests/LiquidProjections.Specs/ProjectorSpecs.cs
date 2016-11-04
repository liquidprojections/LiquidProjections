using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Chill;
using FluentAssertions;
using Xunit;

namespace LiquidProjections.Specs
{
    namespace ProjectorSpecs
    {
        public class Given_a_projector_with_an_in_memory_event_source : GivenWhenThen
        {
            protected readonly TaskCompletionSource<long> DispatchedCheckpointSource = new TaskCompletionSource<long>();
            protected EventMapBuilder<ProjectionContext> Events;

            public Given_a_projector_with_an_in_memory_event_source()
            {
                Given(() =>
                {
                    UseThe(new MemoryEventSource());

                    Events = new EventMapBuilder<ProjectionContext>();

                    UseThe(new Projector(Events));

                    var dispatcher = new Dispatcher(The<MemoryEventSource>());

                    dispatcher.Subscribe(0, async transactions =>
                    {
                        await The<Projector>().Handle(transactions);
                        DispatchedCheckpointSource.SetResult(transactions.Last().Checkpoint);
                    });
                });
            }
        }

        public class When_an_event_requires_a_custom_action :
            Given_a_projector_with_an_in_memory_event_source
        {
            private string discontinuedCategory;
            private ProjectionContext context;
            private long checkpoint;

            public When_an_event_requires_a_custom_action()
            {
                Given(() =>
                {
                    Events.Map<CategoryDiscontinuedEvent>().As((@event, context) =>
                    {
                        discontinuedCategory = @event.Category;
                        this.context = context;

                        return Task.FromResult(false);
                    });
                });

                When(async () =>
                {
                    await The<MemoryEventSource>().Write(new Transaction
                    {
                        Checkpoint = 111,
                        Id = "MyTransactionId",
                        StreamId = "MySTreamId",
                        TimeStampUtc = 10.April(1979).At(13, 14, 15),
                        Headers = new Dictionary<string, object>
                        {
                            {"My custom header", "My custom header value"}
                        },
                        Events = new List<EventEnvelope>
                        {
                            new EventEnvelope
                            {
                                Body = new CategoryDiscontinuedEvent
                                {
                                    Category = "Hybrid"
                                }
                            }
                        }
                    });
                });
            }

            [Fact]
            public async Task Then_it_should_have_executed_the_custom_action()
            {
                await DispatchedCheckpointSource.Task;

                discontinuedCategory.Should().Be("Hybrid");
            }

            [Fact]
            public async Task Then_it_should_have_created_the_context()
            {
                await DispatchedCheckpointSource.Task;

                context.ShouldBeEquivalentTo(new ProjectionContext
                {
                    Checkpoint = 111,
                    TransactionId = "MyTransactionId",
                    StreamId = "MySTreamId",
                    TimeStampUtc = 10.April(1979).At(13, 14, 15),
                    TransactionHeaders = new Dictionary<string, object>
                    {
                        {"My custom header", "My custom header value"}
                    },
                });
            }
        }

        public class When_an_event_is_not_mapped_at_all :
            Given_a_projector_with_an_in_memory_event_source
        {
            private Action action;

            public When_an_event_is_not_mapped_at_all()
            {
                When(async () =>
                {
                    await The<MemoryEventSource>().Write(new ProductAddedToCatalogEvent
                    {
                        ProductKey = "c350E",
                        Category = "Hybrid"
                    });

                    action = async () => await DispatchedCheckpointSource.Task;
                });
            }

            [Fact]
            public void Then_it_should_not_fail()
            {
                action.ShouldNotThrow();
            }
        }

        public class When_an_event_has_a_header : Given_a_projector_with_an_in_memory_event_source
        {
            readonly ProductCatalogEntry projection = new ProductCatalogEntry();

            public When_an_event_has_a_header()
            {
                Given(() =>
                {
                    Events.Map<ProductAddedToCatalogEvent>()
                        .As((anEvent, context) =>
                        {
                            projection.Category = anEvent.Category;
                            projection.AddedBy = (string) context.EventHeaders["UserName"];
                        });
                });

                When(() => The<MemoryEventSource>().WriteWithHeaders(
                    new ProductAddedToCatalogEvent
                    {
                        ProductKey = "c350E",
                        Category = "Hybrid"
                    },
                    new Dictionary<string, object>
                    {
                        ["UserName"] = "Pavel"
                    }));
            }

            [Fact]
            public void Then_it_should_use_the_header()
            {
                projection.AddedBy.Should().Be("Pavel");
            }
        }

        public class When_a_transaction_has_a_header : Given_a_projector_with_an_in_memory_event_source
        {
            readonly ProductCatalogEntry projection = new ProductCatalogEntry();

            public When_a_transaction_has_a_header()
            {
                Given(() =>
                {
                    Events.Map<ProductAddedToCatalogEvent>()
                        .As((anEvent, context) =>
                        {
                            projection.Category = anEvent.Category;
                            projection.AddedBy = (string) context.TransactionHeaders["UserName"];
                        });
                });

                When(() => The<MemoryEventSource>().Write(
                    new Transaction
                    {
                        Events = new[]
                        {
                            new EventEnvelope
                            {
                                Body = new ProductAddedToCatalogEvent
                                {
                                    ProductKey = "c350E",
                                    Category = "Hybrid"
                                }
                            }
                        },
                        Headers = new Dictionary<string, object>
                        {
                            ["UserName"] = "Pavel"
                        }
                    }));
            }

            [Fact]
            public void Then_it_should_use_the_header()
            {
                projection.AddedBy.Should().Be("Pavel");
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
