using System;
using System.Collections.Generic;
using System.Linq;

namespace LiquidProjections.ExampleHost
{
    public class DocumentCountProjection : IEntity
    {
        public DocumentCountProjection()
        {
            Periods = new List<ValidityPeriod>();
        }

        public virtual string Id { get; set; }

        public virtual string Type { get; set; }

        public virtual string Kind { get; set; }

        public virtual string State { get; set; }

        public virtual Guid Country { get; set; }

        public virtual DateTime? NextReviewAt { get; set; }

        public virtual DateTime? LifetimePeriodEnd { get; set; }

        public virtual DateTime? StartDateTime { get; set; }

        public virtual DateTime? EndDateTime { get; set; }

        public List<ValidityPeriod> Periods { get; set; }
        public string RestrictedArea { get; set; }
        public string CountryName { get; set; }

        public ValidityPeriod GetOrAddPeriod(int sequence)
        {
            var period = Periods.FirstOrDefault(p => p.Sequence == sequence);
            if (period == null)
            {
                period = new ValidityPeriod
                {
                    Sequence = sequence
                };

                Periods.Add(period);
            }

            return period;
        }

        public override string ToString()
        {
            return $"Id: {Id}, Kind:{Kind} State:{State}";
        }
    }

    public class ValidityPeriod
    {
        public ValidityPeriod(DateTime? startDateTime, DateTime? endDateTime)
        {
            From = startDateTime;
            To = endDateTime;
        }

        public ValidityPeriod()
        {
        }

        public int Sequence { get; set; }
        public DateTime? From { get; set; }
        public DateTime? To { get; set; }
        public string Status { get; set; }
    }
}