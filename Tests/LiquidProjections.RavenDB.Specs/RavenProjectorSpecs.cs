using System.Linq;
using System.Threading.Tasks;
using Chill;
using FluentAssertions;
using Raven.Client;
using Raven.Client.Embedded;
using Xunit;

namespace LiquidProjections.RavenDB.Specs
{
    namespace RavenProjectorSpecs
    {
        public class Given_an_in_memory_ravendb_and_event_store :
            GivenSubject<ProductCatalogEntry>
        {
            private Transaction transaction;
            private readonly TaskCompletionSource<long> dispatchedCheckpointSource = new TaskCompletionSource<long>();

            public Given_an_in_memory_ravendb_and_event_store()
            {
                Given(() =>
                {
                    UseThe(new MemoryEventSource());

                    IDocumentStore store = new EmbeddableDocumentStore
                    {
                        RunInMemory = true,
                        Configuration =
                        {
                            Storage =
                            {
                            }
                        }
                    }.Initialize();

                    UseThe(store);

                    var dispatcher = new Dispatcher(The<MemoryEventSource>());

                    var map = new EventMapCollection<ProductCatalogEntry, RavenProjectionContext>();
                    map.Map<ProductAddedToCatalogEvent>(e => e.ProductKey, e => e.Version, (p, e) => p.Category = e.Category);

                    var ravenProjector = new RavenProjector<ProductCatalogEntry>(
                        store.OpenAsyncSession, map.GetKey, map.GetVersion, map.GetHandler);

                    dispatcher.Subscribe(0, async transactions =>
                    {
                        await ravenProjector.Handle(transactions);
                        dispatchedCheckpointSource.SetResult(transactions.Last().Checkpoint);
                    });
                });

                When(() =>
                {
                    transaction = The<MemoryEventSource>().Write(new ProductAddedToCatalogEvent
                    {
                        ProductKey = "c350E",
                        Category = "Hybrid",
                        Version = 0
                    });
                });
            }

            [Fact]
            public async Task Then_it_should_update_the_project()
            {
                long lastCheckpoint = await dispatchedCheckpointSource.Task;
                lastCheckpoint.Should().Be(transaction.Checkpoint);

                using (var session = The<IDocumentStore>().OpenAsyncSession())
                {
                    var entry = await session.LoadAsync<ProductCatalogEntry>("c350E");
                    entry.Should().NotBeNull();

                    entry.Category.Should().Be("Hybrid");
                }
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
}