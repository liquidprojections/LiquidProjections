using System;

namespace LiquidProjections.Statistics
{
    public class Event
    {
        public Event(string body, DateTime timestampUtc)
        {
            Body = body;
            TimestampUtc = timestampUtc;
        }

        public string Body { get; }

        public DateTime TimestampUtc { get;  }
    }
}