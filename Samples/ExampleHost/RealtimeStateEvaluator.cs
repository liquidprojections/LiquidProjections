using System;

namespace LiquidProjections.ExampleHost
{
    public class RealtimeStateEvaluator
    {
        public string Evaluate(RealtimeStateEvaluationContext context)
        {
            string state = context.StaticState;

            if (state == "Active")
            {
                DateTime nowAtSite = DateTime.Now;

                state = IsReviewRequired(context.NextReviewAt, nowAtSite)
                    ? "InReview"
                    : DeduceRealtimeState(context, nowAtSite);
            }

            return state;
        }

        private bool IsReviewRequired(DateTime? nextReviewAt, DateTime nowAtSite)
        {
            return nextReviewAt.HasValue && (nowAtSite > nextReviewAt);
        }

        private string DeduceRealtimeState(RealtimeStateEvaluationContext context, DateTime nowAtSite)
        {
            string deducedState = context.StaticState;

            if (nowAtSite < context.StartDateTime)
            {
                deducedState = "AwaitingActivation";
            }

            if (nowAtSite > context.ExpirationDateTime)
            {
                deducedState = "Expired";
            }
            else if (nowAtSite > context.EndDateTime)
            {
                deducedState = "Revalidate";
            }

            return deducedState;
        }
    }

    public class RealtimeStateEvaluationContext
    {
        public ValidityPeriod PlannedPeriod { get; set; }

        public DateTime? ExpirationDateTime { get; set; }

        public string StaticState { get; set; }

        public Guid Country { get; set; }

        public DateTime? NextReviewAt { get; set; }

        public DateTime? StartDateTime => PlannedPeriod?.From;

        public DateTime? EndDateTime => PlannedPeriod?.To;
    }
}
