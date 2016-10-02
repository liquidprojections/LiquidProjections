using System;
using System.Linq;
using Raven.Client.Indexes;

namespace LiquidProjections.ExampleHost
{
    public class Documents_CountsByStaticState :
        AbstractIndexCreationTask<DocumentCountProjection, Documents_CountsByStaticState.Result>
    {
        public class Result
        {
            public Guid Country { get; set; }

            public string CountryName { get; set; }

            public string AuthorizationArea { get; set; }

            public string Kind { get; set; }

            public string State { get; set; }

            public int Count { get; set; }
        }

        public Documents_CountsByStaticState()
        {
            Map = documents =>
                from document in documents
                let dynamicStates = new[] { "Active" }
                where !dynamicStates.Contains(document.State)
                select new Result
                {
                    Country = document.Country,
                    CountryName = LoadDocument<CountryLookup>(document.Country.ToString()).Name,
                    AuthorizationArea = document.RestrictedArea,
                    Kind = document.Kind,
                    State = document.State,
                    Count = 1
                };

            Reduce = results =>
                from r in results
                group r by new { Country = r.Country, r.CountryName, r.AuthorizationArea, r.Kind, r.State }
                into grp
                select new
                {
                    grp.Key.Country,
                    grp.Key.CountryName,
                    grp.Key.AuthorizationArea,
                    grp.Key.Kind,
                    grp.Key.State,
                    Count = grp.Sum(x => x.Count)
                };
        }
    }
}