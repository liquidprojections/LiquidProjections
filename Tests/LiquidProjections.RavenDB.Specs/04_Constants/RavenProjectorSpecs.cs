using System;
using System.Linq;
using System.Threading.Tasks;
using Raven.Client;
using Raven.Client.Embedded;
using Xunit;

namespace LiquidProjections.RavenDB.Specs._04_Constants
{
    public class RavenProjectorSpecs
    {
        [Fact]
        public async Task
            When_a_product_is_discontinued_it_should_remove_the_catalog_entry_from_the_database()
        {
            //----------------------------------------------------------------------------------------------------
            // Arrange
            //----------------------------------------------------------------------------------------------------
            var dispatchedCheckpointSource = new TaskCompletionSource<long>();

            var eventSource = new MemoryEventSource();

            IDocumentStore store = new EmbeddableDocumentStore
            {
                RunInMemory = true,
                Configuration = { RunInUnreliableYetFastModeThatIsNotSuitableForProduction = true }
            }.Initialize();

            var cache = new LruProjectionCache<ProductCatalogEntry>(1000, TimeSpan.Zero, TimeSpan.FromHours(1),
                () => DateTime.Now);

            using (var session = store.OpenAsyncSession())
            {
                await session.StoreAsync(new ProductCatalogEntry
                {
                    Id = "c350E"
                });

                await session.SaveChangesAsync();
            }

            cache.Add(new ProductCatalogEntry
            {
                Id = "c350E",
                Category = "Hybrid"
            });

            var projector = new RavenProjector<ProductCatalogEntry>(store.OpenAsyncSession, cache);
            projector.Map<ProductDiscontinuedEvent>().AsDeleteOf(e => e.ProductKey);

            var dispatcher = new Dispatcher(eventSource);
            dispatcher.Subscribe(0, async transactions =>
            {
                await projector.Handle(transactions);
                dispatchedCheckpointSource.SetResult(transactions.Last().Checkpoint);
            });

            //----------------------------------------------------------------------------------------------------
            // Act
            //----------------------------------------------------------------------------------------------------
            await eventSource.Write(new ProductDiscontinuedEvent
            {
                ProductKey = "c350E",
            });

            //----------------------------------------------------------------------------------------------------
            // Assert
            //----------------------------------------------------------------------------------------------------
            await dispatchedCheckpointSource.Task;

            using (var session = store.OpenAsyncSession())
            {
                var entry = await session.LoadAsync<ProductCatalogEntry>("c350E");
                Assert.Null(entry);
            }

            Assert.Equal(cache.CurrentCount, 0);
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