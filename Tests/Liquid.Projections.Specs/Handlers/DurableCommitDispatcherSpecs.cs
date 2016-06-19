using System;
using System.Threading.Tasks;

namespace eVision.QueryHost.Specs.Handlers
{
    namespace DurableCommitDispatcherSpecs
    {
        public class Given_a_durable_commit_dispatcher : GivenSubject<DurableCommitDispatcher>
        {
            public Given_a_durable_commit_dispatcher()
            {
                Given(() =>
                {
                    UseThe<ICheckpointStore>(new InMemoryCheckpointStore());
                    UseThe(new ProjectorRegistry<IDisposable>());

                    var eventSource = new MemoryEventSource();
                    UseThe<IEventSource>(eventSource);
                    UseThe(eventSource);
                });
            }

            protected override DurableCommitDispatcher BuildSubject()
            {
                var commitDispatcher = new CommitDispatcher<IDisposable>(() => Task.FromResult<IDisposable>(null),
                    _ => Task.FromResult(0), The<ProjectorRegistry<IDisposable>>());

                return new DurableCommitDispatcher(
                    "Dispatcher",
                    The<IEventSource>(),
                    The<ICheckpointStore>(), 500.Milliseconds(),
                    commitDispatcher);
            }
        }

        public class When_a_new_commit_arrives : Given_a_durable_commit_dispatcher
        {
            private ProjectionContext lastProjectionContext;
            private MoreSpecificEvent lastEvent;

            public When_a_new_commit_arrives()
            {
                Given(() =>
                {
                    var projector = new TestProjector<MoreSpecificEvent>((context, @event) =>
                    {
                        lastProjectionContext = context;
                        lastEvent = @event;
                    });

                    The<ProjectorRegistry<IDisposable>>().Add(_ => projector);
                });

                When(async () =>
                {
                    await Subject.Start();

                    await The<MemoryEventSource>().Write(new Transaction
                    { 
                        StreamId = "STREAM",
                        Checkpoint = "999",
                        TimeStamp = 6.June(2015).At(16,14),
                        Events =
                        {
                            new Envelope
                            {
                                Body = new MoreSpecificEvent()
                            }
                        }
                    });
                });
            }

            [Fact]
            public async Task Then_it_should_dispatch_the_commit()
            {
                Transaction commit = The<MemoryEventSource>().Last;
                bool dispatched = await Subject.WaitForDispatchOf(commit.Checkpoint, 5.Seconds());
                dispatched.Should().BeTrue();

                lastProjectionContext.Should().NotBeNull();
                lastProjectionContext.CommitStamp.Should().Be(6.June(2015).At(16, 14));
                lastProjectionContext.CheckPoint.Should().Be("999");
                lastProjectionContext.StreamId.Should().Be("STREAM");
            }

            [Fact]
            public async Task Then_it_should_track_the_last_checkpoint()
            {
                await Task.Delay(2.Seconds());

                string lastCheckpoint = await The<ICheckpointStore>().Get();
                lastCheckpoint.Should().Be("999");
            }
        }

        public class When_handler_throws : Given_a_durable_commit_dispatcher
        {
            private Task<Transaction> transactionProjected;

            public When_handler_throws()
            {
                Given(() => The<ProjectorRegistry<IDisposable>>()
                    .Add(_ => new TestProjector<MoreSpecificEvent>(__ => { throw new Exception(); })));

                When(async () =>
                {
                    await Subject.Start();

                    await The<ICheckpointStore>().Put("OLD");

                    transactionProjected = Subject.DispatchedCommits.Take(1).ToTask();

#pragma warning disable 4014
                    The<MemoryEventSource>().Write(new Transaction
#pragma warning restore 4014
                    {
                        Events =
                        {
                            new Envelope
                            {
                                Body = new MoreSpecificEvent()
                            }
                        }
                    });
                });
            }

            [Fact]
            public void Then_it_should_not_mark_the_commit_as_dispatched()
            {
                Subject.PollNow();

                Func<Task> act = async () => await transactionProjected;

                act.ShouldThrow<Exception>();
            }

            [Fact]
            public async Task Then_it_should_retain_the_previous_checkpoint()
            {
                await Task.Delay(1.Seconds());

                string lastCheckpoint = await The<ICheckpointStore>().Get();
                lastCheckpoint.Should().Be("OLD");
            }
        }

        public class When_no_commits_have_been_dispatched_yet : Given_a_durable_commit_dispatcher
        {
            public When_no_commits_have_been_dispatched_yet()
            {
                Given(() => The<ProjectorRegistry<IDisposable>>()
                    .Add(_ => new TestProjector<MoreSpecificEvent>(__ => { throw new Exception(); })));

                When(async () => { await Subject.Start(); });
            }

            [Fact]
            public async Task Then_waiting_for_a_particular_commit_should_result_in_a_timeout()
            {
                Subject.PollNow();

                bool dispatched = await Subject.WaitForDispatchOf("123", 2.Seconds());
                dispatched.Should().BeFalse("because a timeout should occur");
            }
        }

        public class When_the_dispatcher_isnt_started_yet : Given_a_durable_commit_dispatcher
        {
            public When_the_dispatcher_isnt_started_yet()
            {
                Given(() => The<ProjectorRegistry<IDisposable>>()
                    .Add(_ => new TestProjector<MoreSpecificEvent>(__ => { throw new Exception(); })));
            }

            [Fact]
            public void Then_waiting_for_a_particular_commit_should_throw()
            {
                Subject.PollNow();

                Func<Task> act = () => Subject.WaitForDispatchOf("123", 2.Seconds());
                act.ShouldThrow<InvalidOperationException>().WithMessage("*started yet*");
            }
        }

        public class BaseEvent
        {
            
        }

        public class OtherSpecificEvent : BaseEvent
        {
        }

        public class MoreSpecificEvent : BaseEvent
        {
        }
    }
}