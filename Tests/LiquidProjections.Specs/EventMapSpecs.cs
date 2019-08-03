using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Chill;
using FluentAssertions;
using Xunit;

namespace LiquidProjections.Specs
{
    namespace EventMapSpecs
    {
        public class When_event_should_create_a_new_projection : GivenWhenThen
        {
            private ProductCatalogEntry projection;
            private IEventMap<ProjectionContext> map;

            public When_event_should_create_a_new_projection()
            {
                Given(() =>
                {
                    var mapBuilder = new EventMapBuilder<ProductCatalogEntry, string, ProjectionContext>();
                    mapBuilder.Map<ProductAddedToCatalogEvent>().AsCreateOf(e => e.ProductKey).Using((p, e, ctx) =>
                    {
                        p.Category = e.Category;

                        return Task.FromResult(0);
                    });

                    map = mapBuilder.Build(new ProjectorMap<ProductCatalogEntry, string, ProjectionContext>
                    {
                        Create = async (key, context, projector, shouldOverwrite) =>
                        {
                            projection = new ProductCatalogEntry
                            {
                                Id = key,
                            };

                            await projector(projection);
                        }
                    });
                });

                When(async () =>
                {
                    await map.Handle(new ProductAddedToCatalogEvent
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
        }

        public class When_event_should_create_a_new_projection_from_context : GivenWhenThen
        {
            private ProductCatalogEntry projection;
            private IEventMap<ProjectionContext> map;

            public When_event_should_create_a_new_projection_from_context()
            {
                Given(() =>
                {
                    var mapBuilder = new EventMapBuilder<ProductCatalogEntry, string, ProjectionContext>();
                    mapBuilder.Map<ProductAddedToCatalogEvent>().AsCreateOf((e, context) => context.EventHeaders["ProductId"] as string).Using((p, e, ctx) =>
                     {
                         p.Category = e.Category;

                         return Task.FromResult(0);
                     });

                    map = mapBuilder.Build(new ProjectorMap<ProductCatalogEntry, string, ProjectionContext>
                    {
                        Create = async (key, context, projector, shouldOverwrite) =>
                        {
                            projection = new ProductCatalogEntry
                            {
                                Id = key,
                            };

                            await projector(projection);
                        }
                    });
                });

                When(async () =>
                {
                    await map.Handle(new ProductAddedToCatalogEvent
                    {
                        Category = "Hybrids"
                    },
                        new ProjectionContext() { EventHeaders = new Dictionary<string, object>(1) { { "ProductId", "1234" } } });
                });
            }

            [Fact]
            public void It_should_properly_pass_the_mapping_to_the_creating_handler()
            {
                projection.Should().BeEquivalentTo(new
                {
                    Id = "1234",
                    Category = "Hybrids",
                    Deleted = false
                });
            }
        }

        public class When_a_creating_event_must_ignore_an_existing_projection : GivenWhenThen
        {
            private ProductCatalogEntry existingProjection;

            private IEventMap<ProjectionContext> map;

            public When_a_creating_event_must_ignore_an_existing_projection()
            {
                Given(() =>
                {
                    var mapBuilder = new EventMapBuilder<ProductCatalogEntry, string, ProjectionContext>();

                    mapBuilder
                        .Map<ProductAddedToCatalogEvent>()
                        .AsCreateOf(e => e.ProductKey).IgnoringDuplicates()
                        .Using((p, e, ctx) =>
                        {
                            p.Category = e.Category;
                            return Task.FromResult(0);
                        });

                    existingProjection = new ProductCatalogEntry
                    {
                        Id = "c350E",
                        Category = "Fosile",
                    };

                    map = mapBuilder.Build(new ProjectorMap<ProductCatalogEntry, string, ProjectionContext>
                    {
                        Create = async (key, context, projector, shouldOverwrite) =>
                        {
                            if (shouldOverwrite(existingProjection))
                            {
                                await projector(existingProjection);
                            }
                        }
                    });
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
            public void It_should_leave_the_existing_projection_untouched()
            {
                existingProjection.Should().BeEquivalentTo(new
                {
                    Id = "c350E",
                    Category = "Fosile"
                });
            }
        }

        public class When_a_creating_event_should_overwrite_an_existing_projection : GivenWhenThen
        {
            private ProductCatalogEntry existingProjection;
            private IEventMap<ProjectionContext> map;

            public When_a_creating_event_should_overwrite_an_existing_projection()
            {
                Given(() =>
                {
                    var mapBuilder = new EventMapBuilder<ProductCatalogEntry, string, ProjectionContext>();

                    mapBuilder
                        .Map<ProductAddedToCatalogEvent>()
                        .AsCreateOf(e => e.ProductKey)
                        .OverwritingDuplicates()
                        .Using((p, e, ctx) =>
                        {
                            p.Category = e.Category;
                            return Task.FromResult(0);
                        });

                    existingProjection = new ProductCatalogEntry
                    {
                        Id = "c350E",
                        Category = "OldCategory",
                    };

                    map = mapBuilder.Build(new ProjectorMap<ProductCatalogEntry, string, ProjectionContext>
                    {
                        Create = async (key, context, projector, shouldOverwrite) =>
                        {
                            if (shouldOverwrite(existingProjection))
                            {
                                await projector(existingProjection);
                            }
                        }
                    });
                });

                When(async () =>
                {
                    await map.Handle(
                        new ProductAddedToCatalogEvent
                        {
                            Category = "NewCategory",
                            ProductKey = "c350E"
                        },
                        new ProjectionContext());
                });
            }

            [Fact]
            public void It_should_tell_the_creating_handler_to_overwrite_the_existing_proejction()
            {
                existingProjection.Should().BeEquivalentTo(new
                {
                    Id = "c350E",
                    Category = "NewCategory",
                });
            }
        }

        public class When_a_creating_event_should_allow_manual_handling_of_duplicates : GivenWhenThen
        {
            private ProductCatalogEntry existingProjection;
            private IEventMap<ProjectionContext> map;
            private ProductCatalogEntry duplicateProjection;

            public When_a_creating_event_should_allow_manual_handling_of_duplicates()
            {
                Given(() =>
                {
                    var mapBuilder = new EventMapBuilder<ProductCatalogEntry, string, ProjectionContext>();

                    mapBuilder
                        .Map<ProductAddedToCatalogEvent>()
                        .AsCreateOf(e => e.ProductKey)
                        .HandlingDuplicatesUsing((duplicate, @event, context) =>
                        {
                            duplicateProjection = existingProjection;
                            return true;
                        })
                        .Using((p, e, ctx) =>
                        {
                            p.Category = e.Category;
                            return Task.FromResult(0);
                        });

                    existingProjection = new ProductCatalogEntry
                    {
                        Id = "c350E",
                        Category = "OldCategory",
                    };

                    map = mapBuilder.Build(new ProjectorMap<ProductCatalogEntry, string, ProjectionContext>
                    {
                        Create = async (key, context, projector, shouldOverwrite) =>
                        {
                            if (shouldOverwrite(existingProjection))
                            {
                                await projector(existingProjection);
                            }
                        }
                    });
                });

                When(async () =>
                {
                    await map.Handle(new ProductAddedToCatalogEvent
                    {
                        Category = "NewCategory",
                        ProductKey = "c350E"
                    },
                        new ProjectionContext());
                });
            }

            [Fact]
            public void It_should_honor_the_custom_handlings_wish_to_overwrite()
            {
                existingProjection.Should().BeEquivalentTo(new
                {
                    Id = "c350E",
                    Category = "NewCategory",
                });
            }

            [Fact]
            public void It_should_pass_the_duplicate_to_the_handler()
            {
                duplicateProjection.Should().BeSameAs(existingProjection);
            }
        }

        public class When_an_updating_event_should_throw_on_misses : GivenWhenThen
        {
            private ProductCatalogEntry existingProjection;
            private IEventMap<ProjectionContext> map;

            public When_an_updating_event_should_throw_on_misses()
            {
                Given(() =>
                {
                    var mapBuilder = new EventMapBuilder<ProductCatalogEntry, string, ProjectionContext>();
                    mapBuilder.Map<ProductAddedToCatalogEvent>().AsUpdateOf(e => e.ProductKey).Using((p, e, ctx) =>
                    {
                        p.Category = e.Category;

                        return Task.FromResult(0);
                    });

                    existingProjection = new ProductCatalogEntry
                    {
                        Id = "c350E",
                        Category = "OldCategory",
                    };

                    map = mapBuilder.Build(new ProjectorMap<ProductCatalogEntry, string, ProjectionContext>
                    {
                        Update = async (key, context, projector, createIfMissing) =>
                        {
                            if (createIfMissing())
                            {
                                await projector(existingProjection);
                            }
                        }
                    });
                });

                WhenLater(async () =>
                {
                    await map.Handle(
                        new ProductAddedToCatalogEvent
                        {
                            ProductKey = "c350E",
                            Category = "NewCategory"
                        },
                        new ProjectionContext());
                });
            }

            [Fact]
            public void It_should_throw_without_affecting_the_projection()
            {
                WhenAction.Should().Throw<ProjectionException>();

                existingProjection.Category.Should().Be("OldCategory");
            }
        }

        public class When_an_updating_event_should_ignore_missing_projections : GivenWhenThen
        {
            private IEventMap<ProjectionContext> map;

            public When_an_updating_event_should_ignore_missing_projections()
            {
                Given(() =>
                {
                    var mapBuilder = new EventMapBuilder<ProductCatalogEntry, string, ProjectionContext>();

                    mapBuilder
                        .Map<ProductAddedToCatalogEvent>()
                        .AsUpdateOf(e => e.ProductKey)
                        .IgnoringMisses()
                        .Using((p, e, ctx) =>
                        {
                            p.Category = e.Category;
                            return Task.FromResult(0);
                        });

                    map = mapBuilder.Build(new ProjectorMap<ProductCatalogEntry, string, ProjectionContext>
                    {
                        Update = (key, context, projector, createIfMissing) => Task.FromResult(false)
                    });
                });

                WhenLater(async () =>
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
            public void It_should_not_throw()
            {
                WhenAction.Should().NotThrow();
            }
        }

        public class When_an_updating_event_should_create_a_missing_projection : GivenWhenThen
        {
            private IEventMap<ProjectionContext> map;
            private bool shouldCreate;

            public When_an_updating_event_should_create_a_missing_projection()
            {
                Given(() =>
                {
                    var mapBuilder = new EventMapBuilder<ProductCatalogEntry, string, ProjectionContext>();

                    mapBuilder
                        .Map<ProductAddedToCatalogEvent>()
                        .AsUpdateOf(e => e.ProductKey)
                        .CreatingIfMissing()
                        .Using((p, e, ctx) =>
                        {
                            p.Category = e.Category;
                            return Task.FromResult(0);
                        });

                    map = mapBuilder.Build(new ProjectorMap<ProductCatalogEntry, string, ProjectionContext>
                    {
                        Update = (key, context, projector, createIfMissing) =>
                        {
                            shouldCreate = true;

                            return Task.FromResult(0);
                        }
                    });
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
            public void It_should_not_throw()
            {
                shouldCreate.Should().BeTrue("because that's how the map was configured");
            }
        }

        public class When_an_updating_event_should_create_a_missing_projection_from_context : GivenWhenThen
        {
            private IEventMap<ProjectionContext> map;
            private bool shouldCreate;

            public When_an_updating_event_should_create_a_missing_projection_from_context()
            {
                Given(() =>
                {
                    var mapBuilder = new EventMapBuilder<ProductCatalogEntry, string, ProjectionContext>();

                    mapBuilder
                        .Map<ProductAddedToCatalogEvent>()
                        .AsUpdateOf((e, context) => context.EventHeaders["ProductId"] as string)
                        .CreatingIfMissing()
                        .Using((p, e, ctx) =>
                        {
                            p.Category = e.Category;
                            return Task.FromResult(0);
                        });

                    map = mapBuilder.Build(new ProjectorMap<ProductCatalogEntry, string, ProjectionContext>
                    {
                        Update = (key, context, projector, createIfMissing) =>
                        {
                            shouldCreate = true;

                            return Task.FromResult(0);
                        }
                    });
                });

                When(async () =>
                {
                    await map.Handle(
                        new ProductAddedToCatalogEvent
                        {
                            Category = "Hybrids",
                            ProductKey = "c350E"
                        },
                        new ProjectionContext() { EventHeaders = new Dictionary<string, object>(1) { { "ProductId", "1234" } } });
                });
            }

            [Fact]
            public void It_should_not_throw()
            {
                shouldCreate.Should().BeTrue("because that's how the map was configured");
            }
        }

        public class When_an_updating_event_should_handle_misses_manually : GivenWhenThen
        {
            private IEventMap<ProjectionContext> map;
            private string missedKey;

            public When_an_updating_event_should_handle_misses_manually()
            {
                Given(() =>
                {
                    var mapBuilder = new EventMapBuilder<ProductCatalogEntry, string, ProjectionContext>();

                    mapBuilder
                        .Map<ProductAddedToCatalogEvent>()
                        .AsUpdateOf(e => e.ProductKey)
                        .HandlingMissesUsing((key, context) =>
                        {
                            missedKey = key;
                            return true;
                        })
                        .Using((p, e, ctx) =>
                        {
                            p.Category = e.Category;
                            return Task.FromResult(0);
                        });

                    map = mapBuilder.Build(new ProjectorMap<ProductCatalogEntry, string, ProjectionContext>
                    {
                        Update = (key, context, projector, createIfMissing) =>
                        {
                            createIfMissing();
                            return Task.FromResult(0);
                        }
                    });
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
            public void It_should_give_the_custom_handler_a_chance_to_handle_it()
            {
                missedKey.Should().Be("c350E");
            }
        }

        public class When_an_event_is_mapped_as_a_delete : GivenWhenThen
        {
            private IEventMap<ProjectionContext> map;
            private bool isDeleted;

            public When_an_event_is_mapped_as_a_delete()
            {
                Given(() =>
                {
                    var mapBuilder = new EventMapBuilder<ProductCatalogEntry, string, ProjectionContext>();
                    mapBuilder
                        .Map<ProductDiscontinuedEvent>()
                        .AsDeleteOf(e => e.ProductKey)
                        .ThrowingIfMissing();

                    map = mapBuilder.Build(new ProjectorMap<ProductCatalogEntry, string, ProjectionContext>
                    {
                        Delete = (key, context) =>
                        {
                            isDeleted = true;
                            return Task.FromResult(true);
                        }
                    });
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
                isDeleted.Should().BeTrue();
            }
        }

        public class When_deleting_a_non_existing_event_should_be_ignored : GivenWhenThen
        {
            private IEventMap<ProjectionContext> map;

            public When_deleting_a_non_existing_event_should_be_ignored()
            {
                Given(() =>
                {
                    var mapBuilder = new EventMapBuilder<ProductCatalogEntry, string, ProjectionContext>();
                    mapBuilder
                        .Map<ProductDiscontinuedEvent>()
                        .AsDeleteOf(e => e.ProductKey)
                        .IgnoringMisses();

                    map = mapBuilder.Build(new ProjectorMap<ProductCatalogEntry, string, ProjectionContext>
                    {
                        Delete = (key, context) => Task.FromResult(false)
                    });
                });

                WhenLater(async () =>
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
            public void It_should_not_throw()
            {
                WhenAction.Should().NotThrow();
            }
        }

        public class When_deleting_a_non_existing_event_should_be_handled_manually : GivenWhenThen
        {
            private IEventMap<ProjectionContext> map;
            private object missedKey;

            public When_deleting_a_non_existing_event_should_be_handled_manually()
            {
                Given(() =>
                {
                    var mapBuilder = new EventMapBuilder<ProductCatalogEntry, string, ProjectionContext>();
                    mapBuilder
                        .Map<ProductDiscontinuedEvent>()
                        .AsDeleteOf(e => e.ProductKey)
                        .HandlingMissesUsing((key, context) =>
                        {
                            missedKey = key;
                        });

                    map = mapBuilder.Build(new ProjectorMap<ProductCatalogEntry, string, ProjectionContext>
                    {
                        Delete = (key, context) => Task.FromResult(false)
                    });
                });

                WhenLater(async () =>
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
            public void It_should_not_throw()
            {
                WhenAction.Should().NotThrow();
            }
        }

        public class When_deleting_a_non_existing_event_should_be_handled_manually_from_context : GivenWhenThen
        {
            private IEventMap<ProjectionContext> map;
            private object missedKey;

            public When_deleting_a_non_existing_event_should_be_handled_manually_from_context()
            {
                Given(() =>
                {
                    var mapBuilder = new EventMapBuilder<ProductCatalogEntry, string, ProjectionContext>();
                    mapBuilder
                        .Map<ProductDiscontinuedEvent>()
                        .AsDeleteOf((e, context) => context.EventHeaders["ProductId"] as string)
                        .HandlingMissesUsing((key, context) =>
                        {
                            missedKey = key;
                        });

                    map = mapBuilder.Build(new ProjectorMap<ProductCatalogEntry, string, ProjectionContext>
                    {
                        Delete = (key, context) => Task.FromResult(false)
                    });
                });

                WhenLater(async () =>
                {
                    await map.Handle(
                        new ProductDiscontinuedEvent
                        {
                            ProductKey = "c350E"
                        },
                        new ProjectionContext() { EventHeaders = new Dictionary<string, object>(1) { { "ProductId", "1234" } } });
                });
            }

            [Fact]
            public void It_should_not_throw()
            {
                WhenAction.Should().NotThrow();
            }
        }

        public class When_an_event_is_mapped_as_a_custom_action : GivenWhenThen
        {
            private IEventMap<object> map;
            private string involvedKey;

            public When_an_event_is_mapped_as_a_custom_action()
            {
                Given(() =>
                {
                    var mapBuilder = new EventMapBuilder<object>();
                    mapBuilder.Map<ProductDiscontinuedEvent>().As((@event, context) =>
                    {
                        involvedKey = @event.ProductKey;

                        return Task.FromResult(0);
                    });

                    map = mapBuilder.Build(new ProjectorMap<object>
                    {
                        Custom = (context, projector) => projector()
                    });
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

        public class When_a_global_filter_is_not_met : GivenWhenThen
        {
            private string involvedKey = null;
            private IEventMap<object> map;

            public When_a_global_filter_is_not_met()
            {
                Given(() =>
                {
                    var mapBuilder = new EventMapBuilder<object>()
                        .Where((@event, context) =>
                        {
                            if (@event is ProductAddedToCatalogEvent addedEvent)
                            {
                                return Task.FromResult(addedEvent.Category == "Electric");
                            }

                            return Task.FromResult(true);
                        });

                    mapBuilder
                        .Map<ProductAddedToCatalogEvent>()
                        .As((e, ctx) =>
                        {
                            involvedKey = e.ProductKey;

                            return Task.FromResult(0);
                        });

                    map = mapBuilder.Build(new ProjectorMap<object>
                    {
                        Custom = (context, projector) => projector()
                    });
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
                involvedKey.Should().BeNull();
            }
        }

        public class When_a_condition_is_not_met : GivenWhenThen
        {
            private string involvedKey = null;
            private IEventMap<object> map;

            public When_a_condition_is_not_met()
            {
                Given(() =>
                {
                    var mapBuilder = new EventMapBuilder<object>();
                    mapBuilder
                        .Map<ProductAddedToCatalogEvent>()
                        .When(e => e.Category == "Electric")
                        .As((e, ctx) =>
                        {
                            involvedKey = e.ProductKey;

                            return Task.FromResult(0);
                        });

                    map = mapBuilder.Build(new ProjectorMap<object>
                    {
                        Custom = (context, projector) => projector()
                    });
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
                involvedKey.Should().BeNull();
            }
        }

        public class When_a_condition_is_met : GivenWhenThen
        {
            private string involvedKey;
            private IEventMap<object> map;

            public When_a_condition_is_met()
            {
                Given(() =>
                {
                    var mapBuilder = new EventMapBuilder<object>();
                    mapBuilder
                        .Map<ProductAddedToCatalogEvent>()
                        .When(e => e.Category == "Hybrids")
                        .As((e, ctx) =>
                        {
                            involvedKey = e.ProductKey;
                            return Task.FromResult(0);
                        });

                    map = mapBuilder.Build(new ProjectorMap<object>
                    {
                        Custom = (context, projector) => projector()
                    });
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
                involvedKey.Should().Be("c350E");
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
                            .As((e, ctx) =>
                            {
                            });

                        var map = mapBuilder.Build(new ProjectorMap<object>
                        {
                            Custom = (context, projector) => throw new InvalidOperationException("Custom action should not be called.")
                        });
                    };
                });
            }

            [Fact]
            public void It_should_allow_all_of_them()
            {
                action.Should().NotThrow();
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

                    mapBuilder.Map<ProductDiscontinuedEvent>().As((@event, context) =>
                    {
                        involvedKey = @event.ProductKey;

                        return Task.FromResult(0);
                    });

                    map = mapBuilder.Build(new ProjectorMap<ProductCatalogEntry, string, ProjectionContext>
                    {
                        Custom = (context, projector) =>
                        {
                            customActionDecoratorExecuted = true;
                            return projector();
                        }
                    });
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

        public class When_a_global_filter_is_not_met_on_a_projection : GivenWhenThen
        {
            private ProductCatalogEntry projection;
            private IEventMap<ProjectionContext> map;

            public When_a_global_filter_is_not_met_on_a_projection()
            {
                Given(() =>
                {
                    var mapBuilder = new EventMapBuilder<ProductCatalogEntry, string, ProjectionContext>()
                        .Where((@event, context) =>
                        {
                            if (@event is ProductAddedToCatalogEvent addedEvent)
                            {
                                return Task.FromResult(addedEvent.Category == "Electric");
                            }

                            return Task.FromResult(true);
                        });

                    mapBuilder
                        .Map<ProductAddedToCatalogEvent>()
                        .AsUpdateOf(e => e.ProductKey)
                        .Using((p, e, ctx) =>
                        {
                            p.Category = e.Category;

                            return Task.FromResult(0);
                        });

                    map = mapBuilder.Build(new ProjectorMap<ProductCatalogEntry, string, ProjectionContext>
                    {
                        Update = async (key, context, projector, createIfMissing) =>
                        {
                            projection = new ProductCatalogEntry
                            {
                                Id = key,
                            };

                            await projector(projection);
                        }
                    });
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

        public class When_a_condition_is_not_met_on_a_projection : GivenWhenThen
        {
            private ProductCatalogEntry projection;
            private IEventMap<ProjectionContext> map;

            public When_a_condition_is_not_met_on_a_projection()
            {
                Given(() =>
                {
                    var mapBuilder = new EventMapBuilder<ProductCatalogEntry, string, ProjectionContext>();

                    mapBuilder
                        .Map<ProductAddedToCatalogEvent>()
                        .When(e => e.Category == "Electric")
                        .AsUpdateOf(e => e.ProductKey)
                        .Using((p, e, ctx) =>
                        {
                            p.Category = e.Category;

                            return Task.FromResult(0);
                        });

                    map = mapBuilder.Build(new ProjectorMap<ProductCatalogEntry, string, ProjectionContext>
                    {
                        Update = async (key, context, projector, createIfMissing) =>
                        {
                            projection = new ProductCatalogEntry
                            {
                                Id = key,
                            };

                            await projector(projection);
                        }
                    });
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

                    mapBuilder
                        .Map<ProductAddedToCatalogEvent>()
                        .When(e => e.Category == "Hybrids")
                        .AsUpdateOf(e => e.ProductKey)
                        .Using((p, e, ctx) =>
                        {
                            p.Category = e.Category;
                            return Task.FromResult(0);
                        });

                    map = mapBuilder.Build(new ProjectorMap<ProductCatalogEntry, string, ProjectionContext>
                    {
                        Update = async (key, context, projector, createIfMissing) =>
                        {
                            projection = new ProductCatalogEntry
                            {
                                Id = key,
                            };

                            await projector(projection);
                        }
                    });
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

                        mapBuilder.Map<ProductAddedToCatalogEvent>()
                            .When(e => e.Category == "Hybrids")
                            .AsUpdateOf(e => e.ProductKey).Using((p, e, ctx) => p.Category = e.Category);

                        mapBuilder.Map<ProductAddedToCatalogEvent>()
                            .When(e => e.Category == "Electrics")
                            .AsDeleteOf(e => e.ProductKey);

                        var map = mapBuilder.Build(new ProjectorMap<ProductCatalogEntry, string, ProjectionContext>());
                    };
                });
            }

            [Fact]
            public void It_should_allow_all_of_them()
            {
                action.Should().NotThrow();
            }
        }

        public class When_an_event_is_mapped_by_a_base_class_and_an_event_of_child_type_is_handled : GivenWhenThen
        {
            private IEventMap<ProjectionContext> map;

            private ProductCatalogEntry projection;

            public When_an_event_is_mapped_by_a_base_class_and_an_event_of_child_type_is_handled()
            {
                Given(() =>
                {
                    var mapBuilder = new EventMapBuilder<ProductCatalogEntry, string, ProjectionContext>();

                    mapBuilder
                        .Map<ProductEvent>()
                        .AsUpdateOf(e => e.ProductKey)
                        .Using((p, e, ctx) =>
                        {
                            p.Category = "All Products";
                            return Task.FromResult(0);
                        });

                    map = mapBuilder.Build(new ProjectorMap<ProductCatalogEntry, string, ProjectionContext>
                    {
                        Update = async (key, context, projector, createIfMissing) =>
                        {
                            projection = new ProductCatalogEntry
                            {
                                Id = key,
                            };

                            await projector(projection);
                        }
                    });
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
            public void It_should_invoke_the_handler_configured_on_the_base_class()
            {
                projection.Category.Should().Be("All Products");
            }
        }

        public class When_mappings_for_the_base_and_concrete_event_types_are_both_configured : GivenWhenThen
        {
            private IEventMap<ProjectionContext> map;

            private ProductCatalogEntry projection;

            private Type lastHandlerInvoked;

            public When_mappings_for_the_base_and_concrete_event_types_are_both_configured()
            {
                Given(() =>
                {
                    var mapBuilder = new EventMapBuilder<ProductCatalogEntry, string, ProjectionContext>();

                    var inMemoryProjections = new List<ProductCatalogEntry>();

                    mapBuilder
                        .Map<ProductEvent>()
                        .AsUpdateOf(e => e.ProductKey)
                        .Using((p, e, ctx) =>
                        {
                            p.Category = "All Products";
                            lastHandlerInvoked = typeof(ProductEvent);
                            return Task.FromResult(0);
                        });

                    mapBuilder
                        .Map<ProductPriceChangedEvent>()
                        .AsUpdateOf(e => e.ProductKey)
                        .Using((p, e, ctx) =>
                        {
                            p.Price = e.Price;
                            lastHandlerInvoked = typeof(ProductPriceChangedEvent);
                            return Task.FromResult(0);
                        });

                    map = mapBuilder.Build(new ProjectorMap<ProductCatalogEntry, string, ProjectionContext>
                    {
                        Update = async (key, context, projector, createIfMissing) =>
                        {
                            projection = inMemoryProjections.FirstOrDefault(projection => projection.Id == key);

                            if (projection == null)
                            {
                                projection = new ProductCatalogEntry
                                {
                                    Id = key,
                                };
                                inMemoryProjections.Add(projection);
                            }

                            await projector(projection);
                        }
                    });
                });

                When(async () =>
                {
                    await map.Handle(
                        new ProductPriceChangedEvent
                        {
                            Price = 10.5m,
                            ProductKey = "c350E"
                        },
                        new ProjectionContext());
                });
            }

            [Fact]
            public void It_should_invoke_both_handlers()
            {
                projection.Should().BeEquivalentTo(new
                {
                    Id = "c350E",
                    Category = "All Products",
                    Price = 10.5m,
                    Deleted = false
                });
            }

            [Fact]
            public void It_should_invoke_the_concrete_type_handler_last()
            {
                lastHandlerInvoked.Should().Be<ProductPriceChangedEvent>();
            }
        }

        public class ProductCatalogEntry
        {
            public string Id { get; set; }
            public string Category { get; set; }
            public bool Deleted { get; set; }
            public decimal Price { get; set; }
        }

        public class ProductAddedToCatalogEvent : ProductEvent
        {
            public string Category { get; set; }
        }

        public class ProductDiscontinuedEvent : ProductEvent
        {
        }

        public class ProductPriceChangedEvent : ProductEvent
        {
            public decimal Price { get; set; }
        }

        public class ProductEvent
        {
            public string ProductKey { get; set; }
        }
    }
}