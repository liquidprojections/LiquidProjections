using System;

using eVision.QueryHost.Dispatching;

namespace eVision.QueryHost.Raven.Dispatching
{
    /// <summary>
    /// Builder to simplify the creation of the <see cref="DurableCommitDispatcher"/> for a RavenDb session based projectors.
    /// </summary>
    public class RavenDurableCommitDispatcherBuilder
    {
        private IEventSource eventSource;
        private Func<IWritableRavenSession> ravenSessionFactory;
        private string name = Guid.NewGuid().ToString("N");
        private ProjectorRegistry<IWritableRavenSession> projectorRegistry = new ProjectorRegistry<IWritableRavenSession>();
        private TimeSpan checkpointStorageInterval = TimeSpan.FromSeconds(10);

        /// <summary>
        /// Registers a projector.
        /// </summary>
        /// <param name="projectorFactory">Factory to instantiate the projector based on session.</param>
        public RavenDurableCommitDispatcherBuilder WithProjector<TProjector>(
            Func<IWritableRavenSession, TProjector> projectorFactory) where TProjector : class
        {
            projectorRegistry.Add(projectorFactory);
            return this;
        }

        /// <summary>
        /// Registers multiple projectors.
        /// </summary>
        /// <param name="registry">A register of projectors to be used by the dispatcher.</param>
        public RavenDurableCommitDispatcherBuilder WithProjectors(ProjectorRegistry<IWritableRavenSession> registry)
        {
            projectorRegistry = registry;
            return this;
        }

        /// <summary>
        /// Names the dispatcher collection. Used by the checkpoint repository to distinguish queues.
        /// </summary>
        /// <param name="name">Name of the checkpoint storage.</param>
        public RavenDurableCommitDispatcherBuilder Named(string name)
        {
            this.name = name;
            return this;
        }

        /// <summary>
        /// Connects the dispatcher to EventStore.
        /// </summary>
        /// <param name="eventStoreClient">The event store client.</param>
        public RavenDurableCommitDispatcherBuilder ListeningTo(IEventSource eventStoreClient)
        {
            this.eventSource = eventStoreClient;
            return this;
        }

        /// <summary>
        /// Factory to resolve the RavenDB session.
        /// </summary>
        /// <param name="sessionFactory">Factory that can open a session for dispatcher.</param>
        public RavenDurableCommitDispatcherBuilder ResolvesSession(Func<IWritableRavenSession> sessionFactory)
        {
            ravenSessionFactory = sessionFactory;
            return this;
        }

        /// <summary>
        /// Sets the interval at which the last processed checkpoint is persisted.
        /// </summary>
        public RavenDurableCommitDispatcherBuilder StoringCheckpointEvery(TimeSpan timeSpan)
        {
            checkpointStorageInterval = timeSpan;
            return this;
        }

        /// <summary>
        /// Creates an instance based on provided settings/parameters.
        /// </summary>
        public DurableCommitDispatcher Build()
        {
            return new DurableCommitDispatcher(
                name,
                eventSource,
                new RavenCheckpointStore(name, ravenSessionFactory),
                checkpointStorageInterval,
                new CommitDispatcher<IWritableRavenSession>(
                    ravenSessionFactory,
                    session => session.SaveChanges(),
                    projectorRegistry));
        }
    }
}