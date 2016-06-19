using System.Linq;
using System.Threading.Tasks;

namespace eVision.QueryHost.Specs.Queries
{
    public class ExampleQueryHandler : IQueryHandler<ExampleQuery, Result>
    {
        public Task<Result> Handle(ExampleQuery query)
        {
            return Task.FromResult(new Result
            {
                ConvertedValues = query.Values.Select(v => v.ToUpper()).ToList(),
                Count = query.Values.Count
            });
        }
    }
}