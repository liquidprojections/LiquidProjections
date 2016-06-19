using Raven.Client;

namespace eVision.QueryHost.Raven
{
    /// <summary>
    /// Represents a factory of <see cref="RavenSession"/>s. 
    /// </summary>
    public abstract class RavenSessionFactory<TSession> : IRavenSessionFactory<TSession>
        where TSession : RavenSession
    {
        private readonly IDocumentStore documentStore;

        /// <summary>
        /// Creates a new instance of session factory.
        /// </summary>
        /// <param name="documentStore"></param>
        protected RavenSessionFactory(IDocumentStore documentStore)
        {
            this.documentStore = documentStore;
        }

        /// <summary>
        /// Creates a new session.
        /// </summary>
        public TSession Create()
        {
            return CreateNew(DocumentStore);
        }

        /// <summary>
        /// Starts a new unit-of-work that tracks changes made the entities obtained through the associated repositories.
        /// </summary>
        /// <returns>
        /// Always creates a new unit-of-work, regardless of an existing one that is associated with the current thread.
        /// </returns>
        protected abstract TSession CreateNew(IDocumentStore documentStore);

        /// <summary>
        /// Raven <see cref="IDocumentStore"/>.
        /// </summary>
        protected virtual IDocumentStore DocumentStore
        {
            get { return documentStore; }
        }

        /// <summary>
        /// Disposes internal document store.
        /// </summary>
        public void Dispose()
        {
            DocumentStore.Dispose();
        }
    }
}