using System.Threading.Tasks;

namespace eVision.QueryHost.Dispatching
{
    public interface IProject<in TEvent>
    {
        Task Handle(TEvent @event, ProjectionContext context);
    }
}