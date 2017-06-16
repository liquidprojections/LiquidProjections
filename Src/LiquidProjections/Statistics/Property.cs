using System;

namespace LiquidProjections.Statistics
{
    public class Property
    {
        public string Value { get; }

        public DateTime TimestampUtc { get; }

        public Property(string value, DateTime timestampUtc)
        {
            Value = value;
            TimestampUtc = timestampUtc;
        }
    }
}