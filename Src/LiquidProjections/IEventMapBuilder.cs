using System;
using System.Threading.Tasks;

namespace LiquidProjections
{
    public interface IEventMapBuilder<in TContext>
    {
        IEventMap<TContext> Build();
    }

    public interface IEventMapBuilder<in TProjection, out TKey, TContext> : IEventMapBuilder<TContext>
    {
        void HandleCreatesAs(CreateHandler<TKey, TContext, TProjection> handler);

        void HandleUpdatesAs(UpdateHandler<TKey, TContext, TProjection> handler);

        void HandleDeletesAs(DeleteHandler<TKey, TContext> handler);
    }

    public delegate Task CreateHandler<in TKey, TContext, out TProjection>(
        TKey key,
        TContext context,
        Func<TProjection, TContext, Task> projector);

    public delegate Task UpdateHandler<in TKey, TContext, out TProjection>(
        TKey key,
        TContext context,
        Func<TProjection, TContext, Task> projector);

    public delegate Task DeleteHandler<in TKey, in TContext>(TKey key, TContext context);
}