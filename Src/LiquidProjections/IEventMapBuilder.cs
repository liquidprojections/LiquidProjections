using System;
using System.Threading.Tasks;

namespace LiquidProjections
{
    public interface IEventMapBuilder<in TProjection, TContext>
    {
        void HandleUpdatesAs(UpdateHandler<TContext, TProjection> handler);

        void HandleDeletesAs(DeleteHandler<TContext> handler);

        void HandleCustomActionsAs(CustomHandler<TContext> handler);

        IEventMap<TContext> Build();
    }

    public delegate Task UpdateHandler<TContext, out TProjection>(string key, TContext context, Func<TProjection, TContext, Task> projector);

    public delegate Task DeleteHandler<in TContext>(string key, TContext context);

    public delegate Task CustomHandler<TContext>(TContext context, Func<TContext, Task> projector);
}