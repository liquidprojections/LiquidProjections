using Raven.Client;
using Raven.Client.Embedded;

namespace LiquidProjections.RavenDB.Specs
{
    internal class InMemoryRavenDbBuilder
    {
        public IDocumentStore Build()
        {
            IDocumentStore store = new EmbeddableDocumentStore
            {
                RunInMemory = true,
            }.Initialize();

            return store;
        }
    }
}