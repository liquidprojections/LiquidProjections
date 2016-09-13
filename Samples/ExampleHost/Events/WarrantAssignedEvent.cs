using System;

namespace ExampleHost.Events
{
    public class WarrantAssignedEvent
    {
        public string Number { get; set; }

        public string Kind { get; set; }

        public Guid Country { get; set; }
        public string InitialState { get; set; }
    }
}