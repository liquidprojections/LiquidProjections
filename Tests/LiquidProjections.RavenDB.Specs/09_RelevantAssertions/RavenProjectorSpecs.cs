using System;
using System.Linq;
using System.Threading.Tasks;
using Chill;
using FluentAssertions;
using LiquidProjections.RavenDB.Specs._05_TestDataBuilders;
using Raven.Client;
using Xunit;

namespace LiquidProjections.RavenDB.Specs._09_RelevantAssertions
{
    namespace RavenProjectorSpecs
    {
        public class When_an_event_causes_an_exception : GivenWhenThen
        {
            private TaskCompletionSource<long> dispatchedCheckpointSource;
            private Func<Task> act;

            public When_an_event_causes_an_exception()
            {
                Given(() =>
                {
                    dispatchedCheckpointSource = new TaskCompletionSource<long>();

                    UseThe(new MemoryEventSource());
                    UseThe(new RavenDbBuilder().AsInMemory.Build());

                    var projector = new RavenProjector<ProductCatalogEntry>(The<IDocumentStore>().OpenAsyncSession);

                    projector.Map<ProductDiscontinuedEvent>().As((e, ctx) =>
                    {
                        throw new InvalidOperationException("Can't discontinue already discontinued products.");
                    });

                    var dispatcher = new Dispatcher(The<MemoryEventSource>());
                    dispatcher.Subscribe(0, async transactions =>
                    {
                        await projector.Handle(transactions);
                        dispatchedCheckpointSource.SetResult(transactions.Last().Checkpoint);
                    });
                });

                When(() =>
                {
                    act = async () => await The<MemoryEventSource>().Write(new ProductDiscontinuedEvent
                    {
                        ProductKey = "c350E",
                    });
                });
            }

            [Fact]
            public void It_should_pass_the_exception_back_to_the_event_store()
            {
                act.ShouldThrow<AggregateException>()
                    .WithInnerException<ArgumentException>()
                    .WithInnerMessage("Cannot discontinue already discontinued products");
            }
        }
    }

    public class ProductCatalogEntry : IHaveIdentity
    {
        public string Id { get; set; }
        public string Category { get; set; }
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