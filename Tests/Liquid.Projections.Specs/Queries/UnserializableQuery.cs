using System;

namespace eVision.QueryHost.Specs.Queries
{
    [ApiName("unserializable")]
    public class UnserializableQuery : IQuery<Result>
    {
        private string value;

        public string SomeProperty
        {
            get
            {
                throw new InvalidOperationException();
            }
            set { this.value = value; }
        }
    }
}