using System.Threading.Tasks;
using eVision.QueryHost.Dispatching;
using eVision.QueryHost.Raven.Dispatching;

namespace eVision.QueryHost.Raven.Specs
{
    internal class TestRavenProjectorWithLookup : RavenProjector<TestRavenProjection>,
        IProject<TestEvent>,
        IProject<TestLookupChangedEvent>
    {
        public TestRavenProjectorWithLookup(IWritableRavenSession session) : base(session)
        {
            WithLookup<TestRavenProjectionLookup>(p => p.LookupProperty);
        }

        public Task Handle(TestEvent @event, ProjectionContext context)
        {
            return OnHandle(@event.Name, @event.Version, projection =>
            {
                projection.Name = @event.Name;
                projection.LookupProperty = @event.LookupProperty;
            });
        }

        public Task Handle(TestLookupChangedEvent @event, ProjectionContext context)
        {
            return OnHandle(@event.Name, @event.Version, projection => { projection.LookupProperty = @event.LookupProperty; });
        }
    }
}