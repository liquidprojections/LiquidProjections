using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using LiquidProjections.Statistics;

namespace LiquidProjections.ExampleHost
{
    /// <summary>
    /// An example implementation of a generic projector that uses the most extensive 
    /// <see cref="EventMapBuilder{TContext}"/> for creates, updates and deletes.
    /// </summary>
    public class ExampleProjector<TProjection> : IExampleProjector
        where TProjection : class, IEntity, new()
    {
        private readonly InMemoryDatabase store;
        private readonly ProjectionStats stats;

        public ExampleProjector(IEventMapBuilder<TProjection, string, ProjectionContext> mapBuilder, InMemoryDatabase store,
            ProjectionStats stats, params IExampleProjector[] childProjectors)
        {
            this.store = store;
            this.stats = stats;
            var map = BuildMapFrom(mapBuilder);

            InnerProjector = new Projector(map, childProjectors.Select(p => p.InnerProjector));
        }

        private IEventMap<ProjectionContext> BuildMapFrom(IEventMapBuilder<TProjection, string, ProjectionContext> mapBuilder)
        {
            mapBuilder.HandleProjectionModificationsAs((key, context, projector, options) =>
            {
                TProjection projection = store.GetRepository<TProjection>().Find(key);
                if (projection == null)
                {
                    projection = new TProjection()
                    {
                        Id = key
                    };

                    store.Add(projection);
                }

                return projector(projection);
            });

            mapBuilder.HandleProjectionDeletionsAs((key, context, options) =>
            {
                store.GetRepository<TProjection>().RemoveByKey(key);

                return Task.FromResult(0);
            });

            mapBuilder.HandleCustomActionsAs((context, projector) => projector());

            return mapBuilder.Build();
        }

        public async Task Handle(IReadOnlyList<Transaction> transactions)
        {
            await InnerProjector.Handle(transactions);

            stats.TrackProgress(Id, transactions.Last().Checkpoint);
        }

        public Projector InnerProjector { get; }

        public string Id { get; set; }
    }

    public interface IExampleProjector
    {
        Projector InnerProjector { get; }
    }
}