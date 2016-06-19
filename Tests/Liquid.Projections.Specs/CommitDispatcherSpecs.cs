#region

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using eVision.QueryHost.Specs.Handlers.DurableCommitDispatcherSpecs;

#endregion

namespace eVision.QueryHost.Specs
{
    namespace CommitDispatcherSpecs
    {
        public class Given_commit_dispatcher : GivenSubject<CommitDispatcher<CompositeDisposable>>
        {
            protected override CommitDispatcher<CompositeDisposable> BuildSubject()
            {
                return new CommitDispatcher<CompositeDisposable>(
                    () => new CompositeDisposable(), _ => Task.FromResult(0),
                    The<ProjectorRegistry<CompositeDisposable>>());
            }
        }

        public class When_dispatching_events : Given_commit_dispatcher
        {
            private ProjectionContext context = null;

            public When_dispatching_events()
            {
                Given(() =>
                {
                    UseThe(new ProjectorRegistry<CompositeDisposable>
                    {
                        x => new ContextProjector(projectorContext => { context = projectorContext; })
                    });
                });

                When(async () =>
                {
                    var transaction = new Transaction
                    {
                        StreamId = "999",
                        Checkpoint = "CHECKPOINT1",
                        Events =
                        {
                            new Envelope
                            {
                                Body = "any event",
                                Headers = new Dictionary<string, object> {{"somekey", "some value"}}
                            }
                        }
                    };

                    await Subject.Dispatch(transaction, new CancellationToken());
                });
            }

            [Fact]
            public void Then_it_should_pass_along_the_context_to_the_projector()
            {
                context.Should().NotBeNull();
                context.Headers.Should().HaveCount(1);
                context.Headers.Should().Contain("somekey", "some value");

                context.CheckPoint.Should().Be("CHECKPOINT1");
                context.StreamId.Should().Be("999");
            }
        }


        internal class ContextProjector : IProject<object>
        {
            private readonly Action<ProjectionContext> onHandleAction;

            public ContextProjector(Action<ProjectionContext> onHandleAction)
            {
                this.onHandleAction = onHandleAction;
            }

            public Task Handle(object @event, ProjectionContext context)
            {
                onHandleAction(context);
                return Task.FromResult(0);
            }
        }

        public class When_a_projector_handles_both_generic_and_specific_events : Given_commit_dispatcher
        {
            private ProjectorWhichHandlesSpecificAndGenericEvents projector;

            public When_a_projector_handles_both_generic_and_specific_events()
            {
                Given(() =>
                {
                    projector = new ProjectorWhichHandlesSpecificAndGenericEvents();

                    UseThe(new ProjectorRegistry<CompositeDisposable>
                    {
                        _ => projector
                    });
                });

                When(async () =>
                {
                    var transaction = new Transaction
                    {
                        Events =
                        {
                            new Envelope
                            {
                                Body = new MoreSpecificEvent(),
                            }
                        }
                    };

                    await Subject.Dispatch(transaction, new CancellationToken());
                });
            }

            [Fact]
            public void Then_it_should_only_invoke_the_specific_handler_for_that_event()
            {
                projector.SpecificallyHandled.Should().BeTrue();
            }
        }

        public class When_a_projector_only_handles_the_base_event : Given_commit_dispatcher
        {
            private ProjectorWhichHandlesSpecificAndGenericEvents projector;

            public When_a_projector_only_handles_the_base_event()
            {
                Given(() =>
                {
                    projector = new ProjectorWhichHandlesSpecificAndGenericEvents();

                    UseThe(new ProjectorRegistry<CompositeDisposable>
                    {
                        _ => projector
                    });
                });

                When(async () =>
                {
                    var transaction = new Transaction
                    {
                        Events =
                        {
                            new Envelope
                            {
                                Body = new OtherSpecificEvent(),
                            }
                        }
                    };

                    await Subject.Dispatch(transaction, new CancellationToken());
                });
            }

            [Fact]
            public void Then_it_should_only_invoke_the_specific_handler_for_that_event()
            {
                projector.SpecificallyHandled.Should().BeFalse();
            }
        }

        internal class ProjectorWhichHandlesSpecificAndGenericEvents :
            IProject<BaseEvent>, IProject<MoreSpecificEvent>
        {
            public Task Handle(BaseEvent @event, ProjectionContext context)
            {
                SpecificallyHandled = false;
                return Task.FromResult(0);
            }

            public Task Handle(MoreSpecificEvent @event, ProjectionContext context)
            {
                SpecificallyHandled = true;
                return Task.FromResult(0);
            }

            public bool? SpecificallyHandled { get; set; }
        }
    }
}