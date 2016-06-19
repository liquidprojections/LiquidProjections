using System;
using System.Collections.Generic;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.PlatformServices;
using eVision.QueryHost.Raven.Dispatching;

using Raven.Abstractions.Data;
using Raven.Client;
using Raven.Client.Indexes;

namespace eVision.QueryHost.Raven.Querying
{
    /// <summary>
    /// Class is responsible for registering indexes and initializing them on RavenDB lazily, per <see cref="IIdentity"/>.
    /// </summary>
    public class RavenLazyIndexInitializer
    {
        static RavenLazyIndexInitializer()
        {
            // Rx-workaround (it has hardcoded assembly reference which prevents ILMerging the assembly)
            PlatformEnlightenmentProvider.Current = new CurrentPlatformEnlightenmentProvider();
        }

        private readonly IDictionary<string, List<AbstractIndexCreationTask>> indexTasksByPrefix = new Dictionary<string, List<AbstractIndexCreationTask>>();

        /// <summary>
        /// Switches context to registration of indexes per projection.
        /// </summary>
        /// <typeparam name="TProjection">Projection to trigger index initialization for.</typeparam>
        public IndexPerProjectionRegisterer<TProjection> For<TProjection>()
            where TProjection : IIdentity
        {
            var key = RavenSession.GetId<TProjection>("");
            if (!indexTasksByPrefix.ContainsKey(key))
            {
                indexTasksByPrefix[key] = new List<AbstractIndexCreationTask>();
            }
            return new IndexPerProjectionRegisterer<TProjection>(indexTasksByPrefix[key], this);
        }

        /// <summary>
        /// Subscribes to <see cref="IDocumentStore"/> changes to create indexes on first touch of collection.
        /// </summary>
        /// <param name="documentStore"></param>
        /// <returns>Class that can dispose all subscriptions.</returns>
        public IDisposable SubscribeTo(IDocumentStore documentStore)
        {
            var disposable = new CompositeDisposable();
            foreach (KeyValuePair<string, List<AbstractIndexCreationTask>> entityIndexes in indexTasksByPrefix)
            {
                List<AbstractIndexCreationTask> indexes = entityIndexes.Value;

                disposable.Add(
                    documentStore.Changes()
                        .ForDocumentsStartingWith(entityIndexes.Key)
                        .Where(x => (x.Type & DocumentChangeTypes.Common) != DocumentChangeTypes.None)
                        .Take(1)
                        .Subscribe(change =>
                        {
                            foreach (AbstractIndexCreationTask index in indexes)
                            {
                                index.Execute(documentStore);
                            }
                        }));
            }

            return disposable;
        }

        /// <summary>
        /// Same index registration, just in a context of exact projection.
        /// </summary>
        public class IndexPerProjectionRegisterer<TProjection>
            where TProjection : IIdentity
        {
            private readonly List<AbstractIndexCreationTask> indexes;
            private readonly RavenLazyIndexInitializer parent;

            internal IndexPerProjectionRegisterer(List<AbstractIndexCreationTask> indexes, RavenLazyIndexInitializer parent)
            {
                this.indexes = indexes;
                this.parent = parent;
            }

            /// <summary>
            /// Registers an index for a 
            /// </summary>
            /// <typeparam name="TIndex"></typeparam>
            /// <returns></returns>
            public IndexPerProjectionRegisterer<TProjection> Add<TIndex>()
                where TIndex : AbstractIndexCreationTask, new()
            {
                indexes.Add(new TIndex());
                return this;
            }

            /// <summary>
            /// Switches context to registration of indexes per projection.
            /// </summary>
            /// <typeparam name="TAnotherProjection">Projection to trigger index initialization for.</typeparam>
            public IndexPerProjectionRegisterer<TAnotherProjection> For<TAnotherProjection>()
                where TAnotherProjection : IIdentity
            {
                return parent.For<TAnotherProjection>();
            }

            /// <summary>
            /// Subscribes to <see cref="IDocumentStore"/> changes to create indexes on first touch of collection.
            /// </summary>
            /// <param name="documentStore"></param>
            /// <returns>Subscription that can be cancelled.</returns>
            public IDisposable SubscribeTo(IDocumentStore documentStore)
            {
                return parent.SubscribeTo(documentStore);
            }
        }
    }
}