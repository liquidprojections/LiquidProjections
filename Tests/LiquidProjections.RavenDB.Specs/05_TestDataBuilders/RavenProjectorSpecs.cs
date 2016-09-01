using System.Linq;
using System.Threading.Tasks;
using Raven.Client;
using Xunit;

namespace LiquidProjections.RavenDB.Specs._05_TestDataBuilders
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

            IDocumentStore store = await new RavenDbBuilder()
                .AsInMemory
                .Containing(new ProductCatalogEntryBuilder().IdentifiedBy("c350E").Build())
                .Build();

            var cache = new LruProjectionCacheBuilder()
                .Containing(new ProductCatalogEntryBuilder().IdentifiedBy("c350E").Build())
                .Build<ProductCatalogEntry>();

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