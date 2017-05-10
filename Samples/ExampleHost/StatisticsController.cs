using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Web.Http;

namespace LiquidProjections.ExampleHost
{
    [RoutePrefix("Statistics")]
    public class StatisticsController : ApiController
    {
        private readonly InMemoryDatabase database;

        public StatisticsController(InMemoryDatabase database)
        {
            this.database = database;
        }

        [Route("{CountsPerState}")]
        [HttpGet]
        public dynamic GetCountsPerState(Guid country, string kind)
        {
            var query = 
                from document in database.GetRepository<DocumentCountProjection>()
                let dynamicStates = new[] { "Active" }
                where document.Kind == kind && document.Country == country
                where !dynamicStates.Contains(document.State)
                group document by new { 
                    Country = document.Country,
                    document.CountryName,
                    document.RestrictedArea,
                    document.Kind,
                    document.State }
                into grp
                select new Result
                {
                    Country = grp.Key.Country,
                    CountryName = grp.Key.CountryName,
                    AuthorizationArea = grp.Key.RestrictedArea,
                    Kind = grp.Key.Kind,
                    State = grp.Key.State,
                    Count = grp.Count()
                };

            string countryName = database
                .GetRepository<CountryLookup>()
                .Single(x => x.Id == country.ToString()).Name;

            List<Result> staticResults = query.ToList();

            var dynamicResults =
                from document in database.GetRepository<DocumentCountProjection>()
                let dynamicStates = new[] {"Active"}
                where document.Kind == kind && document.Country == country
                where dynamicStates.Contains(document.State)
                select document;

                var evaluator = new RealtimeStateEvaluator();
            
                foreach (var document in dynamicResults.ToArray())
                {
                    var actualState = evaluator.Evaluate(new RealtimeStateEvaluationContext
                    {
                        StaticState = document.State,
                        Country = document.Country,
                        NextReviewAt = document.NextReviewAt,
                        PlannedPeriod = new ValidityPeriod(document.StartDateTime, document.EndDateTime),
                        ExpirationDateTime = document.LifetimePeriodEnd
                    });
            
                    var result = staticResults.SingleOrDefault(r => r.State == actualState);
                    if (result == null)
                    {
                        result = new Result
                        {
                            Country = document.Country,
                            CountryName = countryName,
                            AuthorizationArea = document.RestrictedArea,
                            Kind = document.Kind,
                            State = document.State,
                            Count = 0
                        };

                        staticResults.Add(result);
                    }

                    result.Count++;
                }

            return staticResults;
        }

        public class Result
        {
            public Guid Country { get; set; }

            public string CountryName { get; set; }

            public string AuthorizationArea { get; set; }

            public string Kind { get; set; }

            public string State { get; set; }

            public int Count { get; set; }
        }
    }
}