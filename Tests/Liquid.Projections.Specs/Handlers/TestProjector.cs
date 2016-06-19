using System;
using System.Threading.Tasks;
using eVision.QueryHost.Dispatching;

namespace eVision.QueryHost.Specs.Handlers
{
    public class TestProjector<TEvent> : IProject<TEvent>
    {
        private readonly Action<ProjectionContext, TEvent> project;

        public TestProjector(Action<TEvent> project)
        {
            this.project = (_, @event) => project(@event);
        }

        public TestProjector(Action<ProjectionContext, TEvent> project)
        {
            this.project = project;
        }

        public Task Handle(TEvent @event, ProjectionContext context)
        {
            project(context, @event);
            return Task.FromResult(0);
        }
    }
}