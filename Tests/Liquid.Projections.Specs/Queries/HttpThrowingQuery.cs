using System.Net;
using eVision.QueryHost.Client;

namespace eVision.QueryHost.Specs.Queries
{
    [ApiName("HttpThrowing")]
    public class HttpThrowingQuery : IQuery<Result>
    {
        public HttpStatusCode Status { get; set; }
        public string Message { get; set; }
    }
}