using System.Collections.Generic;

namespace QueryHost.TestWebHost
{
    public class OwinEvent
    {
        public string RequestBody { get; set; }
        public IDictionary<string, string[]> RequestHeaders { get; set; }
        public string RequestMethod { get; set; }
        public string RequestPath { get; set; }
        public string RequestPathBase { get; set; }
        public string RequestProtocol { get; set; }
        public string RequestQueryString { get; set; }
        public string RequestScheme { get; set; }
    }
}