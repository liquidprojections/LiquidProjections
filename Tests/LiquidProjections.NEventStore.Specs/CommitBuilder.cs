using System;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using NEventStore;
using NEventStore.Persistence;

namespace LiquidProjections.NEventStore.Specs
{
    public class CommitBuilder : TestDataBuilder<Commit>
    {
        private readonly List<EventMessage> events = new List<EventMessage>();
        private DateTime timeStamp = 29.February(2000).At(19, 45);
        private string streamId = Guid.NewGuid().ToString();
        private int sequence = ++nextSequence;
        private static int nextSequence = 1;
        private string checkpointToken = "1";
        private Guid commitId = Guid.NewGuid();

        protected override Commit OnBuild()
        {
            if (!events.Any())
            {
                WithEvent(new TestEvent
                {
                    Version = 1
                });
            }

            long streamRevision = events.Max(e => ((Event) e.Body).Version);
            return new Commit("default", streamId, (int) streamRevision, commitId, sequence, timeStamp, checkpointToken,
                new Dictionary<string, object>(), events);
        }

        public CommitBuilder WithStreamId(string streamId)
        {
            this.streamId = streamId;
            return this;
        }

        public CommitBuilder WithEvents(params Event[] events)
        {
            return events.Select(WithEvent).Last();
        }

        public CommitBuilder WithEvent(Event @event)
        {
            var eventMessage = new EventMessageBuilder().WithBody(@event).Build();

            events.Add(eventMessage);

            return this;
        }

        public CommitBuilder WithEvent(EventMessageBuilder eventMessageBuilder)
        {
            events.Add(eventMessageBuilder.Build());
            return this;
        }

        public CommitBuilder At(DateTime timeStamp)
        {
            this.timeStamp = timeStamp;
            return this;
        }

        public CommitBuilder On(string streamId)
        {
            this.streamId = streamId;
            return this;
        }

        public CommitBuilder WithSequence(int sequence)
        {
            this.sequence = sequence;
            return this;
        }

        public CommitBuilder WithCheckpoint(string checkpointToken)
        {
            this.checkpointToken = checkpointToken;
            return this;
        }

        public CommitBuilder WithCommitId(Guid commitId)
        {
            this.commitId = commitId;
            return this;
        }
    }

    public class TestEvent : Event
    {
    }
}