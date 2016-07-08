using NEventStore;

namespace LiquidProjections.NEventStore.Specs
{
    public class EventMessageBuilder : TestDataBuilder<EventMessage>
    {
        private readonly EventMessage message = new EventMessage();

        protected override EventMessage OnBuild()
        {
            if (message.Body == null)
            {
                message.Body = new object();
            }

            return message;
        }

        public EventMessageBuilder WithBody(Event body)
        {
            message.Body = body;
            return this;
        }

        public EventMessageBuilder WithHeader(string key, object value)
        {
            message.Headers.Add(key, value);
            return this;
        }
    }
}