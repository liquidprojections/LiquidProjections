using eVision.QueryHost.Raven.Dispatching;

namespace eVision.QueryHost.Raven.Specs
{
    internal class TestRavenProjection : IIdentity, IHaveVersion
    {
        public string Name { get; set; }
        public string LookupProperty { get; set; }
        public long Version { get; set; }
        public string Id { get; set; }
    }

    internal class TestRavenProjectionLookup : RavenLookup { }
}