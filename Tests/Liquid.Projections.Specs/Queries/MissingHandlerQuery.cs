namespace eVision.QueryHost.Specs.Queries
{
    [ApiName("missinghandler")]
    public class MissingHandlerQuery : IQuery<Result>
    {
        public string SomeProperty { get; set; }
    }
}