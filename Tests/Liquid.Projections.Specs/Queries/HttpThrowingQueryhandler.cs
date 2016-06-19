using System.Threading.Tasks;
using eVision.QueryHost.Client;

namespace eVision.QueryHost.Specs.Queries
{
    public class HttpThrowingQueryhandler : IQueryHandler<HttpThrowingQuery, Result>
    {
        public Task<Result> Handle(HttpThrowingQuery query)
        {
            throw new QueryException(query.Status, query.Message);
        }
    }
}