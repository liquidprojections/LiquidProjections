using System;
using System.Threading.Tasks;

namespace eVision.QueryHost.NEventStore.Specs
{
    namespace EventStoreClientSpecs
    {
        public class When_the_persistency_engine_is_temporarily_unavailable : GivenSubject<EventStoreClient>
        {
            private readonly TimeSpan pollingInterval = 1.Seconds();
            private Transaction actualTransaction;

            public When_the_persistency_engine_is_temporarily_unavailable()
            {
                Given(() =>
                {
                    UseThe(A.Fake<ICommit>());

                    var eventStore = A.Fake<IPersistStreams>();
                    A.CallTo(() => eventStore.GetFrom(A<string>.Ignored)).Returns(new[] {The<ICommit>()});
                    A.CallTo(() => eventStore.GetFrom(A<string>.Ignored)).Throws(new ApplicationException()).Once();

                    WithSubject(_ => new EventStoreClient(eventStore, (int) pollingInterval.TotalMilliseconds));
                });

                When(() =>
                {
                    Subject.Subscribe("", async transaction =>
                    {
                        actualTransaction = transaction;
                        await Task.Yield();
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

                actualTransaction.Id.Should().Be(The<ICommit>().CommitId);
            }

            protected override void Dispose(bool disposing)
            {
                // SMELL: Chill should dispose our custom subject
                Subject.Dispose();
                base.Dispose(disposing);
            }
        }

        public class When_a_commit_is_persisted : GivenSubject<EventStoreClient>
        {
            private readonly TimeSpan pollingInterval = 1.Seconds();
            private Transaction actualTransaction;

            public When_a_commit_is_persisted()
            {
                Given(() =>
                {
                    UseThe((ICommit) new Fixture().Create<Commit>());

                    var eventStore = A.Fake<IPersistStreams>();
                    A.CallTo(() => eventStore.GetFrom(A<string>.Ignored)).Returns(new[] { The<ICommit>() });

                    WithSubject(_ => new EventStoreClient(eventStore, (int)pollingInterval.TotalMilliseconds));
                });

                When(() =>
                {
                    Subject.Subscribe("", async transaction =>
                    {
                        actualTransaction = transaction;
                        await Task.Yield();
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
                actualTransaction.Id.Should().Be(commit.CommitId);
                actualTransaction.Checkpoint.Should().Be(commit.CheckpointToken);
                actualTransaction.TimeStamp.Should().Be(commit.CommitStamp);
                actualTransaction.StreamId.Should().Be(commit.StreamId);

                actualTransaction.Events.ShouldBeEquivalentTo(commit.Events, options => options.ExcludingMissingMembers() );
            }

            protected override void Dispose(bool disposing)
            {
                // SMELL: Chill should dispose our custom subject
                Subject.Dispose();
                base.Dispose(disposing);
            }

        }
    }
}