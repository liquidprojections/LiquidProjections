using System.Threading.Tasks;
using eVision.QueryHost.Client;
using eVision.QueryHost.InvalidQueriesForTests;

namespace eVision.QueryHost.Specs.Queries
{
    public class ThrowingQueryhandler : IQueryHandler<ThrowingQuery, Result>
    {
        public Task<Result> Handle(ThrowingQuery query)
        {
            throw new SomeBusinessException("Invalid operation");
        }
    }
}