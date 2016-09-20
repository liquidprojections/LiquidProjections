using System;

namespace LiquidProjections.ExampleHost.Events
{
    internal class ValidyPeriodClosedEvent
    {
        public string DocumentNumber { get; set; }
        public int Sequence { get; set; }
        public DateTime ClosedAt { get; set; }
    }
}