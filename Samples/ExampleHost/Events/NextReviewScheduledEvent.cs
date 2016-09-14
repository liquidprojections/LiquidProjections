using System;

namespace ExampleHost.Events
{
    internal class NextReviewScheduledEvent
    {
        public string DocumentNumber { get; set; }
        public DateTime NextReviewAt { get; set; }
    }
}