namespace eVision.QueryHost.Raven.Specs
{
    internal class TestEvent
    {
        public string Name { get; set; }
        public string LookupProperty { get; set; }
        public long Version { get; set; }
    }

    internal class TestLookupChangedEvent
    {
        public string Name { get; set; }
        public string LookupProperty { get; set; }
        public long Version { get; set; }
    }
}