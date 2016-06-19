namespace eVision.QueryHost.Specs.Queries
{
    [ApiName("unserializableresult")]
    public class UnserializableResultQuery : IQuery<UnserializableResult>
    {
        public string SomeProperty { get; set; }
    }
}