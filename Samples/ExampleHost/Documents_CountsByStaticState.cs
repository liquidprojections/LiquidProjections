using System;
using System.Linq;
using Raven.Client.Indexes;

namespace ExampleHost
{
    public class Documents_CountsByStaticState :
        AbstractIndexCreationTask<DocumentCountProjection, Documents_CountsByStaticState.Result>
    {
        public class Result
        {
            public Guid Country { get; set; }

            public string AuthorizationArea { get; set; }

            public string Kind { get; set; }

            public string State { get; set; }

            public int Count { get; set; }
        }

        public Documents_CountsByStaticState()
        {
            Map = workItems =>
                from workItem in workItems
                let dynamicStates = new[] { "Active" }
                where !dynamicStates.Contains(workItem.State)
                select new Result
                {
                    Country = workItem.Country,
                    Kind = workItem.Kind,
                    State = workItem.State,
                    Count = 1
                };

            Reduce = results =>
                from r in results
                group r by new { SiteCode = r.Country, r.AuthorizationArea, r.Kind, r.State }
                into grp
                select new
                {
                    grp.Key.SiteCode,
                    grp.Key.AuthorizationArea,
                    grp.Key.Kind,
                    grp.Key.State,
                    Count = grp.Sum(x => x.Count)
                };
        }
    }
}