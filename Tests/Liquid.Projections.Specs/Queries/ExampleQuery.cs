using System.Collections.Generic;

namespace eVision.QueryHost.Specs.Queries
{
    [ApiName("Example")]
    public class ExampleQuery : IQuery<Result>
    {
        public List<string> Values { get; set; }
    }
}