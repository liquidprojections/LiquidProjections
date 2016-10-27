using System;
using System.Threading.Tasks;
using Chill;
using FluentAssertions;
using Xunit;

namespace LiquidProjections.Specs
{
    namespace EventMapSpecs
    {
        public class When_an_event_is_mapped_as_an_update : GivenWhenThen
        {
            private ProductCatalogEntry projection;
            private IEventMap<ProjectionContext> map;

            public When_an_event_is_mapped_as_an_update()
            {
                Given(() =>
                {
                    var mapBuilder = new EventMapBuilder<ProductCatalogEntry, string, ProjectionContext>();
                    mapBuilder.Map<ProductAddedToCatalogEvent>().AsUpdateOf(e => e.ProductKey).Using((p, e, ctx) =>
                    {
                        p.Category = e.Category;

                        return Task.FromResult(0);
                    });

                    mapBuilder.HandleUpdatesAs(async (key, context, projector) =>
                    {
                        projection = new ProductCatalogEntry
                        {
                            Id = key,
                        };

                        await projector(projection, context);
                    });

                    map = mapBuilder.Build();
                });

                When(async () =>
                {
                    Func<ProjectionContext, Task> handler = map.GetHandler(new ProductAddedToCatalogEvent
                    {
                        Category = "Hybrids",
                        ProductKey = "c350E"
                    });

                    await handler(new ProjectionContext());
                });
            }

            [Fact]
            public void It_should_properly_pass_the_mapping_to_the_updating_handler()
            {
                projection.ShouldBeEquivalentTo(new
                {
                    Id = "c350E",
                    Category = "Hybrids",
                    Deleted = false
                });
            }
        }

        public class When_an_event_is_mapped_as_a_delete : GivenWhenThen
        {
            private ProductCatalogEntry projection;
            private IEventMap<ProjectionContext> map;

            public When_an_event_is_mapped_as_a_delete()
            {
                Given(() =>
                {
                    var mapBuilder = new EventMapBuilder<ProductCatalogEntry, string, ProjectionContext>();
                    mapBuilder.Map<ProductDiscontinuedEvent>().AsDeleteOf(e => e.ProductKey);

                    mapBuilder.HandleDeletesAs((key, context) =>
                    {
                        projection = new ProductCatalogEntry
                        {
                            Id = key,
                            Deleted = true
                        };

                        return Task.FromResult(0);
                    });

                    map = mapBuilder.Build();
                });

                When(async () =>
                {
                    Func<ProjectionContext, Task> handler = map.GetHandler(new ProductDiscontinuedEvent
                    {
                        ProductKey = "c350E"
                    });

                    await handler(new ProjectionContext());
                });
            }

            [Fact]
            public void It_should_properly_pass_the_mapping_to_the_deleting_handler()
            {
                projection.ShouldBeEquivalentTo(new
                {
                    Id = "c350E",
                    Category = (string) null,
                    Deleted = true
                });
            }
        }


        public class When_an_event_is_mapped_as_a_custom_action : GivenWhenThen
        {
            private string involvedKey;
            private IEventMap<ProjectionContext> map;

            public When_an_event_is_mapped_as_a_custom_action()
            {
                Given(() =>
                {
                    var mapBuilder = new EventMapBuilder<ProductCatalogEntry, string, ProjectionContext>();
                    mapBuilder.HandleCustomActionsAs((context, projector) => projector(context));
                    
                    mapBuilder.Map<ProductDiscontinuedEvent>().As((@event, context) =>
                    {
                        involvedKey = @event.ProductKey;

                        return Task.FromResult(0);
                    });

                    map = mapBuilder.Build();
                });

                When(async () =>
                {
                    Func<ProjectionContext, Task> handler = map.GetHandler(new ProductDiscontinuedEvent
                    {
                        ProductKey = "c350E"
                    });

                    await handler(new ProjectionContext());
                });
            }

            [Fact]
            public void It_should_properly_pass_the_mapping_to_the_custom_handler()
            {
                involvedKey.Should().Be("c350E");
            }
        }

        public class When_a_condition_is_not_met : GivenWhenThen
        {
            private ProductCatalogEntry projection;
            private IEventMap<ProjectionContext> map;

            public When_a_condition_is_not_met()
            {
                Given(() =>
                {
                    var mapBuilder = new EventMapBuilder<ProductCatalogEntry, string, ProjectionContext>();
                    mapBuilder.HandleUpdatesAs(async (key, context, projector) =>
                    {
                        projection = new ProductCatalogEntry
                        {
                            Id = key,
                        };

                        await projector(projection, context);
                    });

                    mapBuilder.Map<ProductAddedToCatalogEvent>().When(e => e.Category == "Electric").AsUpdateOf(e => e.ProductKey).Using((p, e, ctx) =>
                    {
                        p.Category = e.Category;

                        return Task.FromResult(0);
                    });

                    map = mapBuilder.Build();
                });

                When(async () =>
                {
                    Func<ProjectionContext, Task> handler = map.GetHandler(new ProductAddedToCatalogEvent
                    {
                        Category = "Hybrids",
                        ProductKey = "c350E"
                    });

                    await handler(new ProjectionContext());
                });
            }

            [Fact]
            public void It_should_not_invoke_any_handler()
            {
                projection.Should().BeNull();
            }
        }

        public class When_a_condition_is_met : GivenWhenThen
        {
            private ProductCatalogEntry projection;
            private IEventMap<ProjectionContext> map;

            public When_a_condition_is_met()
            {
                Given(() =>
                {
                    var mapBuilder = new EventMapBuilder<ProductCatalogEntry, string, ProjectionContext>();
                    mapBuilder.HandleUpdatesAs(async (key, context, projector) =>
                    {
                        projection = new ProductCatalogEntry
                        {
                            Id = key,
                        };

                        await projector(projection, context);
                    });

                    mapBuilder.Map<ProductAddedToCatalogEvent>().When(e => e.Category == "Hybrids").AsUpdateOf(e => e.ProductKey).Using((p, e, ctx) =>
                    {
                        p.Category = e.Category;

                        return Task.FromResult(0);
                    });

                    map = mapBuilder.Build();
                });

                When(async () =>
                {
                    Func<ProjectionContext, Task> handler = map.GetHandler(new ProductAddedToCatalogEvent
                    {
                        Category = "Hybrids",
                        ProductKey = "c350E"
                    });

                    await handler(new ProjectionContext());
                });
            }

            [Fact]
            public void It_should_invoke_the_right_handler()
            {
                projection.ShouldBeEquivalentTo(new
                {
                    Id = "c350E",
                    Category = "Hybrids",
                    Deleted = false
                });
            }
        }

        public class When_multiple_conditions_are_registered : GivenWhenThen
        {
            private Action action;

            public When_multiple_conditions_are_registered()
            {
                When(() =>
                {
                    action = () =>
                    {
                        var mapBuilder = new EventMapBuilder<ProductCatalogEntry, string, ProjectionContext>();

                        mapBuilder.Map<ProductAddedToCatalogEvent>()
                            .When(e => e.Category == "Hybrids")
                            .AsUpdateOf(e => e.ProductKey).Using((p, e, ctx) => p.Category = e.Category);

                        mapBuilder.Map<ProductAddedToCatalogEvent>()
                            .When(e => e.Category == "Electrics")
                            .AsDeleteOf(e => e.ProductKey);

                        var map = mapBuilder.Build();
                    };
                });
            }

            [Fact]
            public void It_should_allow_all_of_them()
            {
                action.ShouldNotThrow();
            }
        }

        public class ProductCatalogEntry
        {
            public string Id { get; set; }
            public string Category { get; set; }
            public bool Deleted { get; set; }
        }

        public class ProductAddedToCatalogEvent
        {
            public string ProductKey { get; set; }
            public string Category { get; set; }

            public long Version { get; set; }
        }

        public class ProductDiscontinuedEvent
        {
            public string ProductKey { get; set; }
        }

        public class CategoryDiscontinuedEvent
        {
            public string Category { get; set; }
        }
    }
}