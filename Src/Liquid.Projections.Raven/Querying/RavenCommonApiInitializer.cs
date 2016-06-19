using Raven.Client;
using Raven.Client.Connection;
using Raven.Client.Document;
using Raven.Client.Indexes;

namespace eVision.QueryHost.Raven.Querying
{
    /// <summary>
    /// Helper class to register indexes and transformers (<see cref="AbstractCommonApiForIndexesAndTransformers"/>) without explicit calls to their methods.
    /// </summary>
    public class RavenCommonApiInitializer
    {
        private readonly IDatabaseCommands databaseCommands;
        private readonly DocumentConvention conventions;

        /// <summary>
        /// Initializes new instance for a document store.
        /// </summary>
        /// <param name="documentStore">RavenDB document store.</param>
        /// <param name="databaseName">An optional name of the database to add the index to.</param>
        public RavenCommonApiInitializer(IDocumentStore documentStore, string databaseName = "")
        {
            conventions = documentStore.Conventions;

            databaseCommands = documentStore.DatabaseCommands;
            if (databaseName.Length > 0)
            {
                databaseCommands = databaseCommands.ForDatabase(databaseName);
            }
        }

        /// <summary>
        /// Executes an index creation task.
        /// </summary>
        /// <typeparam name="TIndexCreator">Static index definition creator class.</typeparam>
        /// <returns>Self</returns>
        public RavenCommonApiInitializer Add<TIndexCreator>() where TIndexCreator : AbstractIndexCreationTask, new()
        {
            new TIndexCreator().Execute(databaseCommands, conventions);
            return this;
        }

        /// <summary>
        /// Executes a transformer creation task.
        /// </summary>
        /// <typeparam name="TTransformerCreator">Static transformer definition creator task.</typeparam>
        /// <returns>Self</returns>
        public RavenCommonApiInitializer AddTransformer<TTransformerCreator>() where TTransformerCreator : AbstractTransformerCreationTask, new()
        {
            new TTransformerCreator().Execute(databaseCommands, conventions);
            return this;
        }
    }
}