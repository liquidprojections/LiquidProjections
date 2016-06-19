using Raven.Client;

namespace eVision.QueryHost.Raven.Specs
{
    public class TestRavenSession : RavenSession
    {
        public TestRavenSession(IAsyncDocumentSession session) : base(session)
        {
        }
    }
}