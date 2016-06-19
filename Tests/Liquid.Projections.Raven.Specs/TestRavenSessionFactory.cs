using eVision.QueryHost.Raven.Dispatching;
using Raven.Abstractions.Data;
using Raven.Client;

namespace eVision.QueryHost.Raven.Specs
{
    public class TestRavenSessionFactory : RavenSessionFactory<TestRavenSession>
    {
        public TestRavenSessionFactory(IDocumentStore documentStore) :
            base(documentStore)
        {
        }

        protected override TestRavenSession CreateNew(IDocumentStore documentStore)
        {
            return new TestRavenSession(documentStore.OpenAsyncSession());
        }

        public IWritableRavenSession CreateBulkWriter()
        {
            return new RavenBulkSession(
                DocumentStore.OpenAsyncSession,
                DocumentStore.BulkInsert(options: new BulkInsertOptions
                {
                    OverwriteExisting = true,
                    SkipOverwriteIfUnchanged = true
                }));
        }
    }
}