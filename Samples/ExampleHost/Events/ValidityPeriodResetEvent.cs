namespace LiquidProjections.ExampleHost.Events
{
    internal class ValidityPeriodResetEvent
    {
        public string DocumentNumber { get; set; }
        public int Sequence { get; set; }
    }
}