using System;
using System.Threading.Tasks;
using Chill;
using FluentAssertions;
using Xunit;

namespace LiquidProjections.Specs
{
    namespace EventMapSpecs
    {
        public class When_an_event_is_mapped_as_a_create : GivenWhenThen
        {
            private ProductCatalogEntry projection;
            private IEventMap<ProjectionContext> map;
            private ProjectionModificationOptions options;

            public When_an_event_is_mapped_as_a_create()
            {
                Given(() =>
                {
                    var mapBuilder = new EventMapBuilder<ProductCatalogEntry, string, ProjectionContext>();
                    mapBuilder.Map<ProductAddedToCatalogEvent>().AsCreateOf(e => e.ProductKey).Using((p, e, ctx) =>
                    {
                        p.Category = e.Category;

                        return Task.FromResult(0);
                    });

                    mapBuilder.HandleProjectionModificationsAs(async (key, context, projector, options) =>
                    {
                        projection = new ProductCatalogEntry
                        {
                            Id = key,
                        };

                        this.options = options;
                        await projector(projection);
                    });

                    mapBuilder.HandleProjectionDeletionsAs((key, context, options) =>
                    {
                        throw new InvalidOperationException("Deletion should not be called.");
                    });

                    mapBuilder.HandleCustomActionsAs((context, projector) =>
                    {
                        throw new InvalidOperationException("Custom action should not be called.");
                    });

                    map = mapBuilder.Build();
                });

                When(async () =>
                {
                    await map.Handle(
                        new ProductAddedToCatalogEvent
                        {
                            Category = "Hybrids",
                            ProductKey = "c350E"
                        },
                        new ProjectionContext());
                });
            }

            [Fact]
            public void It_should_properly_pass_the_mapping_to_the_creating_handler()
            {
                projection.Should().BeEquivalentTo(new
                {
                    Id = "c350E",
                    Category = "Hybrids",
                    Deleted = false
                });
            }

            [Fact]
            public void It_should_create_projection_if_it_does_not_exist()
            {
                options.MissingProjectionBehavior.Should().Be(MissingProjectionModificationBehavior.Create);
            }

            [Fact]
            public void It_should_throw_if_the_projection_already_exists()
            {
                options.ExistingProjectionBehavior.Should().Be(ExistingProjectionModificationBehavior.Throw);
            }
        }

        public class When_an_event_is_mapped_as_a_create_if_does_not_exist : GivenWhenThen
        {
            private ProductCatalogEntry projection;
            private IEventMap<ProjectionContext> map;
            private ProjectionModificationOptions options;

            public When_an_event_is_mapped_as_a_create_if_does_not_exist()
            {
                Given(() =>
                {
                    var mapBuilder = new EventMapBuilder<ProductCatalogEntry, string, ProjectionContext>();

                    mapBuilder
                        .Map<ProductAddedToCatalogEvent>()
                        .AsCreateIfDoesNotExistOf(e => e.ProductKey)
                        .Using((p, e, ctx) =>
                        {
                            p.Category = e.Category;
                            return Task.FromResult(0);
                        });

                    mapBuilder.HandleProjectionModificationsAs(async (key, context, projector, options) =>
                    {
                        projection = new ProductCatalogEntry
                        {
                            Id = key,
                        };

                        this.options = options;
                        await projector(projection);
                    });

                    mapBuilder.HandleProjectionDeletionsAs((key, context, options) =>
                    {
                        throw new InvalidOperationException("Deletion should not be called.");
                    });

                    mapBuilder.HandleCustomActionsAs((context, projector) =>
                    {
                        throw new InvalidOperationException("Custom action should not be called.");
                    });

                    map = mapBuilder.Build();
                });

                When(async () =>
                {
                    await map.Handle(
                        new ProductAddedToCatalogEvent
                        {
                            Category = "Hybrids",
                            ProductKey = "c350E"
                        },
                        new ProjectionContext());
                });
            }

            [Fact]
            public void It_should_properly_pass_the_mapping_to_the_creating_handler()
            {
                projection.Should().BeEquivalentTo(new
                {
                    Id = "c350E",
                    Category = "Hybrids",
                    Deleted = false
                });
            }

            [Fact]
            public void It_should_create_projection_if_it_does_not_exist()
            {
                options.MissingProjectionBehavior.Should().Be(MissingProjectionModificationBehavior.Create);
            }

            [Fact]
            public void It_should_do_nothing_if_the_projection_already_exists()
            {
                options.ExistingProjectionBehavior.Should().Be(ExistingProjectionModificationBehavior.Ignore);
            }
        }

        public class When_an_event_is_mapped_as_a_create_or_update : GivenWhenThen
        {
            private ProductCatalogEntry projection;
            private IEventMap<ProjectionContext> map;
            private ProjectionModificationOptions options;

            public When_an_event_is_mapped_as_a_create_or_update()
            {
                Given(() =>
                {
                    var mapBuilder = new EventMapBuilder<ProductCatalogEntry, string, ProjectionContext>();

                    mapBuilder
                        .Map<ProductAddedToCatalogEvent>()
                        .AsCreateOrUpdateOf(e => e.ProductKey)
                        .Using((p, e, ctx) =>
                        {
                            p.Category = e.Category;
                            return Task.FromResult(0);
                        });

                    mapBuilder.HandleProjectionModificationsAs(async (key, context, projector, options) =>
                    {
                        projection = new ProductCatalogEntry
                        {
                            Id = key,
                        };

                        this.options = options;
                        await projector(projection);
                    });

                    mapBuilder.HandleProjectionDeletionsAs((key, context, options) =>
                    {
                        throw new InvalidOperationException("Deletion should not be called.");
                    });

                    mapBuilder.HandleCustomActionsAs((context, projector) =>
                    {
                        throw new InvalidOperationException("Custom action should not be called.");
                    });

                    map = mapBuilder.Build();
                });

                When(async () =>
                {
                    await map.Handle(
                        new ProductAddedToCatalogEvent
                        {
                            Category = "Hybrids",
                            ProductKey = "c350E"
                        },
                        new ProjectionContext());
                });
            }

            [Fact]
            public void It_should_properly_pass_the_mapping_to_the_creating_handler()
            {
                projection.Should().BeEquivalentTo(new
                {
                    Id = "c350E",
                    Category = "Hybrids",
                    Deleted = false
                });
            }

            [Fact]
            public void It_should_create_projection_if_it_does_not_exist()
            {
                options.MissingProjectionBehavior.Should().Be(MissingProjectionModificationBehavior.Create);
            }

            [Fact]
            public void It_should_update_the_projection_if_it_already_exists()
            {
                options.ExistingProjectionBehavior.Should().Be(ExistingProjectionModificationBehavior.Update);
            }
        }

        public class When_an_event_is_mapped_as_an_update : GivenWhenThen
        {
            private ProductCatalogEntry projection;
            private IEventMap<ProjectionContext> map;
            private ProjectionModificationOptions options;

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

                    mapBuilder.HandleProjectionModificationsAs(async (key, context, projector, options) =>
                    {
                        projection = new ProductCatalogEntry
                        {
                            Id = key,
                        };

                        this.options = options;
                        await projector(projection);
                    });

                    mapBuilder.HandleProjectionDeletionsAs((key, context, options) =>
                    {
                        throw new InvalidOperationException("Deletion should not be called.");
                    });

                    mapBuilder.HandleCustomActionsAs((context, projector) =>
                    {
                        throw new InvalidOperationException("Custom action should not be called.");
                    });

                    map = mapBuilder.Build();
                });

                When(async () =>
                {
                    await map.Handle(
                        new ProductAddedToCatalogEvent
                        {
                            Category = "Hybrids",
                            ProductKey = "c350E"
                        },
                        new ProjectionContext());
                });
            }

            [Fact]
            public void It_should_properly_pass_the_mapping_to_the_updating_handler()
            {
                projection.Should().BeEquivalentTo(new
                {
                    Id = "c350E",
                    Category = "Hybrids",
                    Deleted = false
                });
            }


            [Fact]
            public void It_should_throw_if_the_projection_does_not_exist()
            {
                options.MissingProjectionBehavior.Should().Be(MissingProjectionModificationBehavior.Throw);
            }

            [Fact]
            public void It_should_update_the_projection_if_it_exists()
            {
                options.ExistingProjectionBehavior.Should().Be(ExistingProjectionModificationBehavior.Update);
            }
        }

        public class When_an_event_is_mapped_as_an_update_if_exists : GivenWhenThen
        {
            private ProductCatalogEntry projection;
            private IEventMap<ProjectionContext> map;
            private ProjectionModificationOptions options;

            public When_an_event_is_mapped_as_an_update_if_exists()
            {
                Given(() =>
                {
                    var mapBuilder = new EventMapBuilder<ProductCatalogEntry, string, ProjectionContext>();

                    mapBuilder
                        .Map<ProductAddedToCatalogEvent>()
                        .AsUpdateIfExistsOf(e => e.ProductKey)
                        .Using((p, e, ctx) =>
                        {
                            p.Category = e.Category;
                            return Task.FromResult(0);
                        });

                    mapBuilder.HandleProjectionModificationsAs(async (key, context, projector, options) =>
                    {
                        projection = new ProductCatalogEntry
                        {
                            Id = key,
                        };

                        this.options = options;
                        await projector(projection);
                    });

                    mapBuilder.HandleProjectionDeletionsAs((key, context, options) =>
                    {
                        throw new InvalidOperationException("Deletion should not be called.");
                    });

                    mapBuilder.HandleCustomActionsAs((context, projector) =>
                    {
                        throw new InvalidOperationException("Custom action should not be called.");
                    });

                    map = mapBuilder.Build();
                });

                When(async () =>
                {
                    await map.Handle(
                        new ProductAddedToCatalogEvent
                        {
                            Category = "Hybrids",
                            ProductKey = "c350E"
                        },
                        new ProjectionContext());
                });
            }

            [Fact]
            public void It_should_properly_pass_the_mapping_to_the_updating_handler()
            {
                projection.Should().BeEquivalentTo(new
                {
                    Id = "c350E",
                    Category = "Hybrids",
                    Deleted = false
                });
            }


            [Fact]
            public void It_should_do_nothing_if_the_projection_does_not_exist()
            {
                options.MissingProjectionBehavior.Should().Be(MissingProjectionModificationBehavior.Ignore);
            }

            [Fact]
            public void It_should_update_the_projection_if_it_exists()
            {
                options.ExistingProjectionBehavior.Should().Be(ExistingProjectionModificationBehavior.Update);
            }
        }

        public class When_an_event_is_mapped_as_a_delete : GivenWhenThen
        {
            private ProductCatalogEntry projection;
            private IEventMap<ProjectionContext> map;
            private ProjectionDeletionOptions options;

            public When_an_event_is_mapped_as_a_delete()
            {
                Given(() =>
                {
                    var mapBuilder = new EventMapBuilder<ProductCatalogEntry, string, ProjectionContext>();
                    mapBuilder.Map<ProductDiscontinuedEvent>().AsDeleteOf(e => e.ProductKey);

                    mapBuilder.HandleProjectionDeletionsAs((key, context, options) =>
                    {
                        projection = new ProductCatalogEntry
                        {
                            Id = key,
                            Deleted = true
                        };

                        this.options = options;
                        return Task.FromResult(0);
                    });

                    mapBuilder.HandleProjectionModificationsAs((key, context, projector, options) =>
                    {
                        throw new InvalidOperationException("Modification should not be called.");
                    });

                    mapBuilder.HandleCustomActionsAs((context, projector) =>
                    {
                        throw new InvalidOperationException("Custom action should not be called.");
                    });

                    map = mapBuilder.Build();
                });

                When(async () =>
                {
                    await map.Handle(
                        new ProductDiscontinuedEvent
                        {
                            ProductKey = "c350E"
                        },
                        new ProjectionContext());
                });
            }

            [Fact]
            public void It_should_properly_pass_the_mapping_to_the_deleting_handler()
            {
                projection.Should().BeEquivalentTo(new
                {
                    Id = "c350E",
                    Category = (string) null,
                    Deleted = true
                });
            }

            [Fact]
            public void It_should_throw_if_the_projection_does_not_exist()
            {
                options.MissingProjectionBehavior.Should().Be(MissingProjectionDeletionBehavior.Throw);
            }
        }

        public class When_an_event_is_mapped_as_a_delete_if_exists : GivenWhenThen
        {
            private ProductCatalogEntry projection;
            private IEventMap<ProjectionContext> map;
            private ProjectionDeletionOptions options;

            public When_an_event_is_mapped_as_a_delete_if_exists()
            {
                Given(() =>
                {
                    var mapBuilder = new EventMapBuilder<ProductCatalogEntry, string, ProjectionContext>();
                    mapBuilder.Map<ProductDiscontinuedEvent>().AsDeleteIfExistsOf(e => e.ProductKey);

                    mapBuilder.HandleProjectionDeletionsAs((key, context, options) =>
                    {
                        projection = new ProductCatalogEntry
                        {
                            Id = key,
                            Deleted = true
                        };

                        this.options = options;
                        return Task.FromResult(0);
                    });

                    mapBuilder.HandleProjectionModificationsAs((key, context, projector, options) =>
                    {
                        throw new InvalidOperationException("Modification should not be called.");
                    });

                    mapBuilder.HandleCustomActionsAs((context, projector) =>
                    {
                        throw new InvalidOperationException("Custom action should not be called.");
                    });

                    map = mapBuilder.Build();
                });

                When(async () =>
                {
                    await map.Handle(
                        new ProductDiscontinuedEvent
                        {
                            ProductKey = "c350E"
                        },
                        new ProjectionContext());
                });
            }

            [Fact]
            public void It_should_properly_pass_the_mapping_to_the_deleting_handler()
            {
                projection.Should().BeEquivalentTo(new
                {
                    Id = "c350E",
                    Category = (string)null,
                    Deleted = true
                });
            }

            [Fact]
            public void It_should_do_nothing_if_the_projection_does_not_exist()
            {
                options.MissingProjectionBehavior.Should().Be(MissingProjectionDeletionBehavior.Ignore);
            }
        }

        public class When_an_event_is_mapped_as_a_custom_action : GivenWhenThen
        {
            private string involvedKey;
            private IEventMap<object> map;

            public When_an_event_is_mapped_as_a_custom_action()
            {
                Given(() =>
                {
                    var mapBuilder = new EventMapBuilder<object>();
                    mapBuilder.HandleCustomActionsAs((context, projector) => projector());

                    mapBuilder.Map<ProductDiscontinuedEvent>().As((@event, context) =>
                    {
                        involvedKey = @event.ProductKey;

                        return Task.FromResult(0);
                    });

                    map = mapBuilder.Build();
                });

                When(async () =>
                {
                    await map.Handle(
                        new ProductDiscontinuedEvent
                        {
                            ProductKey = "c350E"
                        },
                        new object());
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
            private readonly ProductCatalogEntry projection = new ProductCatalogEntry
            {
                Id = "c350E",
                Category = "Electrics"
            };
            private IEventMap<object> map;

            public When_a_condition_is_not_met()
            {
                Given(() =>
                {
                    var mapBuilder = new EventMapBuilder<object>();
                    mapBuilder.Map<ProductAddedToCatalogEvent>()
                        .When(e => e.Category == "Electric")
                        .As((e, ctx) =>
                        {
                            projection.Category = e.Category;

                            return Task.FromResult(0);
                        });

                    mapBuilder.HandleCustomActionsAs((context, projector) =>
                    {
                        throw new InvalidOperationException("Custom action should not be called.");
                    });

                    map = mapBuilder.Build();
                });

                When(async () =>
                {
                    await map.Handle(
                        new ProductAddedToCatalogEvent
                        {
                            Category = "Hybrids",
                            ProductKey = "c350E"
                        },
                        new object());
                });
            }

            [Fact]
            public void It_should_not_invoke_any_handler()
            {
                projection.Should().BeEquivalentTo(new
                {
                    Id = "c350E",
                    Category = "Electrics",
                    Deleted = false
                });
            }
        }

        public class When_a_condition_is_met : GivenWhenThen
        {
            private readonly ProductCatalogEntry projection = new ProductCatalogEntry
            {
                Id = "c350E"
            };
            private IEventMap<object> map;

            public When_a_condition_is_met()
            {
                Given(() =>
                {
                    var mapBuilder = new EventMapBuilder<object>();
                    mapBuilder.HandleCustomActionsAs((context, projector) => projector());

                    mapBuilder.Map<ProductAddedToCatalogEvent>()
                        .When(e => e.Category == "Hybrids")
                        .As((e, ctx) =>
                        {
                            projection.Category = e.Category;

                            return Task.FromResult(0);
                        });

                    map = mapBuilder.Build();
                });

                When(async () =>
                {
                    await map.Handle(
                        new ProductAddedToCatalogEvent
                        {
                            Category = "Hybrids",
                            ProductKey = "c350E"
                        },
                        new object());
                });
            }

            [Fact]
            public void It_should_invoke_the_right_handler()
            {
                projection.Should().BeEquivalentTo(new
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
                        var mapBuilder = new EventMapBuilder<object>();

                        mapBuilder.Map<ProductAddedToCatalogEvent>()
                            .When(e => e.Category != "Hybrids")
                            .When(e => e.Category != "Electrics")
                            .As((e, ctx) => {});

                        mapBuilder.HandleCustomActionsAs((context, projector) =>
                        {
                            throw new InvalidOperationException("Custom action should not be called.");
                        });

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

        public class When_an_event_is_mapped_as_a_custom_action_on_a_projection : GivenWhenThen
        {
            private string involvedKey;
            private IEventMap<ProjectionContext> map;
            private bool customActionDecoratorExecuted;

            public When_an_event_is_mapped_as_a_custom_action_on_a_projection()
            {
                Given(() =>
                {
                    var mapBuilder = new EventMapBuilder<ProductCatalogEntry, string, ProjectionContext>();

                    mapBuilder.HandleCustomActionsAs((context, projector) =>
                    {
                        customActionDecoratorExecuted = true;
                        return projector();
                    });

                    mapBuilder.HandleProjectionModificationsAs((key, context, projector, options) =>
                    {
                        throw new InvalidOperationException("Modification should not be called.");
                    });

                    mapBuilder.HandleProjectionDeletionsAs((key, context, options) =>
                    {
                        throw new InvalidOperationException("Deletion should not be called.");
                    });

                    mapBuilder.Map<ProductDiscontinuedEvent>().As((@event, context) =>
                    {
                        involvedKey = @event.ProductKey;

                        return Task.FromResult(0);
                    });

                    map = mapBuilder.Build();
                });

                When(async () =>
                {
                    await map.Handle(
                        new ProductDiscontinuedEvent
                        {
                            ProductKey = "c350E"
                        },
                        new ProjectionContext());
                });
            }

            [Fact]
            public void It_should_properly_pass_the_mapping_to_the_custom_handler()
            {
                involvedKey.Should().Be("c350E");
            }

            [Fact]
            public void It_should_allow_decorating_the_custom_handler()
            {
                customActionDecoratorExecuted.Should().BeTrue();
            }
        }

        public class When_a_condition_is_not_met_on_a_projection : GivenWhenThen
        {
            private ProductCatalogEntry projection;
            private IEventMap<ProjectionContext> map;

            public When_a_condition_is_not_met_on_a_projection()
            {
                Given(() =>
                {
                    var mapBuilder = new EventMapBuilder<ProductCatalogEntry, string, ProjectionContext>();
                    mapBuilder.HandleProjectionModificationsAs(async (key, context, projector, options) =>
                    {
                        projection = new ProductCatalogEntry
                        {
                            Id = key,
                        };

                        await projector(projection);
                    });

                    mapBuilder
                        .Map<ProductAddedToCatalogEvent>()
                        .When(e => e.Category == "Electric")
                        .AsUpdateOf(e => e.ProductKey)
                        .Using((p, e, ctx) =>
                        {
                            p.Category = e.Category;

                            return Task.FromResult(0);
                        });

                    mapBuilder.HandleProjectionDeletionsAs((key, context, options) =>
                    {
                        throw new InvalidOperationException("Deletion should not be called.");
                    });

                    mapBuilder.HandleCustomActionsAs((context, projector) =>
                    {
                        throw new InvalidOperationException("Custom action should not be called.");
                    });

                    map = mapBuilder.Build();
                });

                When(async () =>
                {
                    await map.Handle(
                        new ProductAddedToCatalogEvent
                        {
                            Category = "Hybrids",
                            ProductKey = "c350E"
                        },
                        new ProjectionContext());
                });
            }

            [Fact]
            public void It_should_not_invoke_any_handler()
            {
                projection.Should().BeNull();
            }
        }

        public class When_a_condition_is_met_on_a_projection : GivenWhenThen
        {
            private ProductCatalogEntry projection;
            private IEventMap<ProjectionContext> map;

            public When_a_condition_is_met_on_a_projection()
            {
                Given(() =>
                {
                    var mapBuilder = new EventMapBuilder<ProductCatalogEntry, string, ProjectionContext>();

                    mapBuilder.HandleProjectionModificationsAs(async (key, context, projector, options) =>
                    {
                        projection = new ProductCatalogEntry
                        {
                            Id = key,
                        };

                        await projector(projection);
                    });

                    mapBuilder.HandleProjectionDeletionsAs((key, context, options) =>
                    {
                        throw new InvalidOperationException("Deletion should not be called.");
                    });

                    mapBuilder.HandleCustomActionsAs((context, projector) =>
                    {
                        throw new InvalidOperationException("Custom action should not be called.");
                    });

                    mapBuilder
                        .Map<ProductAddedToCatalogEvent>()
                        .When(e => e.Category == "Hybrids")
                        .AsUpdateOf(e => e.ProductKey)
                        .Using((p, e, ctx) =>
                        {
                            p.Category = e.Category;
                            return Task.FromResult(0);
                        });

                    map = mapBuilder.Build();
                });

                When(async () =>
                {
                    await map.Handle(
                        new ProductAddedToCatalogEvent
                        {
                            Category = "Hybrids",
                            ProductKey = "c350E"
                        },
                        new ProjectionContext());
                });
            }

            [Fact]
            public void It_should_invoke_the_right_handler()
            {
                projection.Should().BeEquivalentTo(new
                {
                    Id = "c350E",
                    Category = "Hybrids",
                    Deleted = false
                });
            }
        }

        public class When_multiple_conditions_are_registered_on_a_projection : GivenWhenThen
        {
            private Action action;

            public When_multiple_conditions_are_registered_on_a_projection()
            {
                When(() =>
                {
                    action = () =>
                    {
                        var mapBuilder = new EventMapBuilder<ProductCatalogEntry, string, ProjectionContext>();

                        mapBuilder.HandleProjectionModificationsAs((key, context, projector, options) =>
                        {
                            throw new InvalidOperationException("Modification should not be called.");
                        });

                        mapBuilder.HandleProjectionDeletionsAs((key, context, options) =>
                        {
                            throw new InvalidOperationException("Deletion should not be called.");
                        });

                        mapBuilder.HandleCustomActionsAs((context, projector) =>
                        {
                            throw new InvalidOperationException("Custom action should not be called.");
                        });

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
        }

        public class ProductDiscontinuedEvent
        {
            public string ProductKey { get; set; }
        }
    }
}