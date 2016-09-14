using System;
using System.Web.Http;

namespace ExampleHost
{
    [RoutePrefix("Statistics")]
    public class StatisticsController : ApiController
    {
        [Route("{CountsPerState}")]
        [HttpGet]
        public dynamic GetCountsPerState(Guid siteCode, string workItemKind)
        {
            return new
            {
                siteCode,
                workItemKind
            };
        }
    }
}