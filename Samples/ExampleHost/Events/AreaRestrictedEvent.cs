namespace ExampleHost.Events
{
    internal class AreaRestrictedEvent
    {
        public string DocumentNumber { get; set; }
        public object Area { get; set; }
    }
}