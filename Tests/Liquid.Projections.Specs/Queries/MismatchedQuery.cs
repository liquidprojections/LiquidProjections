namespace eVision.QueryHost.Specs.Queries
{
    [ApiName("mismatched")]
    public class MismatchedQuery : IQuery<Result>
    {
        public string OtherProperty { get; set; }
    }
}