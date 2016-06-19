using System.Threading.Tasks;
using eVision.QueryHost.Dispatching;
using eVision.QueryHost.Raven.Dispatching;

namespace eVision.QueryHost.Raven.Specs
{
    internal class TestRavenProjector : RavenProjector<TestRavenProjection>,
        IProject<TestEvent>
    {
        public TestRavenProjector(IWritableRavenSession session) : base(session) { }

        public Task Handle(TestEvent @event, ProjectionContext context)
        {
            return OnHandle(@event.Name, @event.Version, projection =>
            {
                projection.Name = @event.Name;
            });
        }
    }
}