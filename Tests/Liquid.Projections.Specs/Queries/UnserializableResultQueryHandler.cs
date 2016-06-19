using System.Threading.Tasks;

namespace eVision.QueryHost.Specs.Queries
{
    public class UnserializableResultQueryHandler : IQueryHandler<UnserializableResultQuery, UnserializableResult>
    {
        public Task<UnserializableResult> Handle(UnserializableResultQuery query)
        {
            return Task.FromResult(new UnserializableResult());
        }
    }
}