using System;

namespace eVision.QueryHost.Specs.Queries
{
    public class UnserializableResult
    {
        private string value;

        public string Value
        {
            get
            {
                throw new InvalidOperationException();
            }
            set { this.value = value; }
        }
    }
}