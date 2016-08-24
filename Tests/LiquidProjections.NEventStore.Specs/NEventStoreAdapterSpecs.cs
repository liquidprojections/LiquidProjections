using System;
using System.Linq;
using System.Threading.Tasks;
using Chill;
using FakeItEasy;
using FluentAssertions;
using NEventStore;
using NEventStore.Persistence;
using Xunit;

namespace LiquidProjections.NEventStore.Specs
{
    namespace EventStoreClientSpecs
    {
        // TODO: Lots of subscriptions for the same checkpoint result in a single query when a new event is pushed
        // TODO: Multiple serialized subscriptions for the same checkpoint result in a single query when executed within the cache window
        // TODO: Multiple serialized subscriptions for the same checkpoint result in a multiple queries when executed outside the cache window
        // TODO: When disposing the adapter, no transactions should be published to subscribers anymore
        // TODO: When disposing the adapter, no queries must happend anymore
        // TODO: When disposing a subscription, no transactions should be published to the subscriber anymore

        public class When_the_persistency_engine_is_temporarily_unavailable : GivenSubject<NEventStoreAdapter>
        {
            private readonly TimeSpan pollingInterval = 1.Seconds();
            private Transaction actualTransaction;

            public When_the_persistency_engine_is_temporarily_unavailable()
            {
                Given(() =>
                {
                    UseThe((ICommit) new CommitBuilder().WithCheckpoint("123").Build());

                    var eventStore = A.Fake<IPersistStreams>();
                    A.CallTo(() => eventStore.GetFrom(A<string>.Ignored)).Returns(new[] {The<ICommit>()});
                    A.CallTo(() => eventStore.GetFrom(A<string>.Ignored)).Throws(new ApplicationException()).Once();

                    WithSubject(_ => new NEventStoreAdapter(eventStore, 11, pollingInterval, 100, () => DateTime.UtcNow));
                });

                When(() =>
                {
                    Subject.Subscribe(null, transactions =>
                    {
                        actualTransaction = transactions.First();

                        return Task.FromResult(0);
                    });
                });
            }

            [Fact]
            public async Task Then_it_should_recover_automatically_after_its_polling_interval_expires()
            {
                do
                {
                    await Task.Delay(pollingInterval);
                }
                while (actualTransaction == null);

                actualTransaction.Id.Should().Be(The<ICommit>().CommitId.ToString());
            }
        }

        public class When_a_commit_is_persisted : GivenSubject<NEventStoreAdapter>
        {
            private readonly TimeSpan pollingInterval = 1.Seconds();
            private Transaction actualTransaction;

            public When_a_commit_is_persisted()
            {
                Given(() =>
                {
                    UseThe((ICommit) new CommitBuilder().WithCheckpoint("123").Build());

                    var eventStore = A.Fake<IPersistStreams>();
                    A.CallTo(() => eventStore.GetFrom(A<string>.Ignored)).Returns(new[] {The<ICommit>()});

                    WithSubject(_ => new NEventStoreAdapter(eventStore, 11, pollingInterval, 100, () => DateTime.UtcNow));
                });

                When(() =>
                {
                    Subject.Subscribe(null, transactions =>
                    {
                        actualTransaction = transactions.First();

                        return Task.FromResult(0);
                    });
                });
            }

            [Fact]
            public async Task Then_it_should_convert_the_commit_details_to_a_transaction()
            {
                do
                {
                    await Task.Delay(pollingInterval);
                }
                while (actualTransaction == null);

                var commit = The<ICommit>();
                actualTransaction.Id.Should().Be(commit.CommitId.ToString());
                actualTransaction.Checkpoint.Should().Be(long.Parse(commit.CheckpointToken));
                actualTransaction.TimeStampUtc.Should().Be(commit.CommitStamp);
                actualTransaction.StreamId.Should().Be(commit.StreamId);

                actualTransaction.Events.ShouldBeEquivalentTo(commit.Events, options => options.ExcludingMissingMembers());
            }
        }
        public class When_there_are_no_more_commits : GivenSubject<NEventStoreAdapter>
        {
            private readonly TimeSpan pollingInterval = 500.Milliseconds();
            private DateTime utcNow = DateTime.UtcNow;
            private IPersistStreams eventStore;
            private TaskCompletionSource<object> eventStoreQueriedSource = new TaskCompletionSource<object>();

            public When_there_are_no_more_commits()
            {
                Given(() =>
                {
                    eventStore = A.Fake<IPersistStreams>();
                    A.CallTo(() => eventStore.GetFrom(A<string>.Ignored))
                    .Invokes(_ =>
                    {
                        eventStoreQueriedSource.SetResult(null);
                    })
                    .Returns(new ICommit[0]);

                    WithSubject(_ => new NEventStoreAdapter(eventStore, 11, pollingInterval, 100, () => utcNow));

                    Subject.Subscribe(1000, transactions => Task.FromResult(0));
                });

                this.WhenAsync(async () =>
                {
                    await eventStoreQueriedSource.Task;
                });
            }

            [Fact]
            public void Then_it_should_wait_for_the_polling_interval_to_retry()
            {
                A.CallTo(() => eventStore.GetFrom(A<string>.Ignored)).MustHaveHappened(Repeated.Exactly.Once);

                utcNow = utcNow.Add(1.Seconds());

                A.CallTo(() => eventStore.GetFrom(A<string>.Ignored)).MustHaveHappened(Repeated.Exactly.Once);
            }
        }
    }
}