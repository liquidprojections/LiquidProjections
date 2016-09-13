namespace ExampleHost.Events
{
    internal class ValidityPeriodApprovedEvent
    {
        public string DocumentNumber { get; set; }
        public int Sequence { get; set; }
    }
}