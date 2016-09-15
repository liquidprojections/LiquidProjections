using System;
using System.Linq;
using System.Threading.Tasks;
using System.Web.Http;
using Raven.Client;

namespace LiquidProjections.ExampleHost
{
    [RoutePrefix("Statistics")]
    public class StatisticsController : ApiController
    {
        private readonly Func<IAsyncDocumentSession> sessionFactory;

        public StatisticsController(Func<IAsyncDocumentSession> sessionFactory)
        {
            this.sessionFactory = sessionFactory;
        }

        [Route("{CountsPerState}")]
        [HttpGet]
        public async Task<dynamic> GetCountsPerState(Guid country, string kind)
        {
            using (var session = sessionFactory())
            {
                var staticResults = await session
                    .Query<Documents_CountsByStaticState.Result, Documents_CountsByStaticState>()
                    .Where(x => x.Kind == kind && x.Country == country)
                    .ToListAsync();

                var stream = session
                    .Query<Documents_ByDynamicState.Result, Documents_ByDynamicState>()
                    .Where(x => x.Kind == kind && x.Country == country)
                    .As<DocumentCountProjection>();

                var evaluator = new RealtimeStateEvaluator();

                var iterator = await session.Advanced.StreamAsync(stream);
                while (await iterator.MoveNextAsync())
                {
                    DocumentCountProjection projection = iterator.Current.Document;
                    var actualState = evaluator.Evaluate(new RealtimeStateEvaluationContext
                    {
                        StaticState = projection.State,
                        Country = projection.Country,
                        NextReviewAt = projection.NextReviewAt,
                        PlannedPeriod = new ValidityPeriod(projection.StartDateTime, projection.EndDateTime),
                        ExpirationDateTime = projection.LifetimePeriodEnd
                    });

                    var result = staticResults.SingleOrDefault(r => r.State == actualState);
                    if (result == null)
                    {
                        result = new Documents_CountsByStaticState.Result
                        {
                            Kind = kind,
                            Country = country,
                            State = actualState,
                        };

                        staticResults.Add(result);
                    }

                    result.Count++;
                }

                return staticResults;
            }
        }
    }
}