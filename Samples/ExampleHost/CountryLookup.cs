using LiquidProjections.RavenDB;

namespace LiquidProjections.ExampleHost
{
    internal class CountryLookup : IHaveIdentity
    {
        public string Id { get; set; }
        public string Name { get; set; }
    }
}