using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using LiquidProjections.ExampleHost.Events;
using LiquidProjections.RavenDB;
using Raven.Client;

namespace LiquidProjections.ExampleHost
{
    public class CountsProjectionBootstrapper
    {
        private readonly Dispatcher dispatcher;
        private readonly Func<IAsyncDocumentSession> sessionFactory;
        private RavenProjector<DocumentCountProjection> projector;
        private readonly Stopwatch stopwatch = new Stopwatch();
        private long eventCount = 0;
        private long transactionCount = 0;
        private readonly EventMapBuilder<DocumentCountProjection, string, RavenProjectionContext> mapBuilder;

        public CountsProjectionBootstrapper(Dispatcher dispatcher, Func<IAsyncDocumentSession> sessionFactory)
        {
            this.dispatcher = dispatcher;
            this.sessionFactory = sessionFactory;
            mapBuilder = BuildEventMap();
        }   

        public async Task Start()
        {
            var lruCache = new LruProjectionCache<DocumentCountProjection>(20000, TimeSpan.FromSeconds(30), TimeSpan.FromMinutes(2),
                () => DateTime.Now);

            projector = new RavenProjector<DocumentCountProjection>(sessionFactory, mapBuilder, batchSize: 20, cache: lruCache);

            long lastCheckpoint = await projector.GetLastCheckpoint() ?? 0;

            stopwatch.Start();

            dispatcher.Subscribe(lastCheckpoint, async transactions =>
            {
                await projector.Handle(transactions);

                transactionCount += transactions.Count;
                eventCount += transactions.Sum(t => t.Events.Count);

                long elapsedTotalSeconds = (long)stopwatch.Elapsed.TotalSeconds;
                if ((transactionCount % 100 == 0) && (elapsedTotalSeconds > 0))
                {
                    int ratePerSecond = (int)(eventCount / elapsedTotalSeconds);

                    Console.WriteLine(
                        $"{DateTime.Now}: Processed {eventCount} events (rate: {ratePerSecond}/second, hits: {lruCache.Hits}, Misses: {lruCache.Misses})");
                }
            });
        }

        private static EventMapBuilder<DocumentCountProjection, string, RavenProjectionContext> BuildEventMap()
        {
            var map = new EventMapBuilder<DocumentCountProjection, string, RavenProjectionContext>();

            map.Map<CountryRegisteredEvent>().As(async (e, ctx) =>
            {
                await ctx.Session.StoreAsync(new CountryLookup
                {
                    Id = e.Code,
                    Name = e.Name
                });
            });

            map.Map<WarrantAssignedEvent>().AsCreateOf(e => e.Number).Using((p, e, ctx) =>
            {
                p.Type = "Warrant";
                p.Kind = e.Kind;
                p.Country = e.Country;
                p.State = e.InitialState;
            });

            map.Map<CertificateIssuedEvent>().AsCreateOf(e => e.Number).Using((p, e, ctx) =>
            {
                p.Type = "Certificate";
                p.Kind = e.Kind;
                p.Country = e.Country;
                p.State = e.InitialState;
            });

            map.Map<ConstitutionEstablishedEvent>().AsUpdateOf(e => e.Number).Using((p, e, ctx) =>
            {
                p.Type = "Constitution";
                p.Kind = e.Kind;
                p.Country = e.Country;
                p.State = e.InitialState;
            });

            map.Map<LicenseGrantedEvent>().AsCreateOf(e => e.Number).Using((p, e, ctx) =>
            {
                p.Type = "Audit";
                p.Kind = e.Kind;
                p.Country = e.Country;
                p.State = e.InitialState;
            });

            map.Map<ContractNegotiatedEvent>().AsCreateOf(e => e.Number).Using((p, e, ctx) =>
            {
                p.Type = "Task";
                p.Kind = e.Kind;
                p.Country = e.Country;
                p.State = e.InitialState;
            });

            map.Map<BondIssuedEvent>().AsCreateOf(e => e.Number).Using((p, e, ctx) =>
            {
                p.Type = "IsolationCertificate";
                p.Kind = e.Kind;
                p.Country = e.Country;
                p.State = e.InitialState;
            });

            map
                .Map<AreaRestrictedEvent>()
                .AsUpdateOf(e => e.DocumentNumber).Using((p, e, ctx) => p.RestrictedArea = e.Area);

            map
                .Map<AreaRestrictionCancelledEvent>()
                .AsUpdateOf(e => e.DocumentNumber).Using((p, e, ctx) => p.RestrictedArea = null);

            map.Map<StateTransitionedEvent>()
                .When(e => e.State != "Closed")
                .AsUpdateOf(e => e.DocumentNumber).Using((p, e, ctx) => p.State = e.State);

            map.Map<StateRevertedEvent>()
                .AsUpdateOf(e => e.DocumentNumber).Using((p, e, ctx) => { p.State = e.State; });

            map.Map<DocumentArchivedEvent>().AsDeleteOf(e => e.DocumentNumber);

            map.Map<CountryCorrectedEvent>().AsUpdateOf(e => e.DocumentNumber).Using((p, e, ctx) => p.Country = e.Country);

            map.Map<NextReviewScheduledEvent>().AsUpdateOf(e => e.DocumentNumber).Using(
                (p, e, ctx) => p.NextReviewAt = e.NextReviewAt);

            map.Map<LifetimeRestrictedEvent>().AsUpdateOf(e => e.DocumentNumber).Using(
                (p, e, ctx) => p.LifetimePeriodEnd = e.PeriodEnd);

            map.Map<LifetimeRestrictionRemovedEvent>().AsUpdateOf(e => e.DocumentNumber).Using(
                (p, e, ctx) => p.LifetimePeriodEnd = null);

            map.Map<ValidityPeriodPlannedEvent>().AsUpdateOf(e => e.DocumentNumber).Using((p, e, ctx) =>
            {
                var period = p.GetOrAddPeriod(e.Sequence);
                period.From = e.From;
                period.To = e.To;
            });

            map.Map<ValidityPeriodResetEvent>().AsUpdateOf(e => e.DocumentNumber).Using((p, e, ctx) =>
            {
                var period = p.GetOrAddPeriod(e.Sequence);
                period.From = null;
                period.To = null;
            });

            map.Map<ValidityPeriodApprovedEvent>().AsUpdateOf(e => e.DocumentNumber).Using((p, e, ctx) =>
            {
                var period = p.GetOrAddPeriod(e.Sequence);
                period.Status = "Valid";

                var lastValidPeriod = p.Periods.LastOrDefault(x => x.Status == "Valid");

                var contiguousPeriods = GetPreviousContiguousValidPeriods(p.Periods, lastValidPeriod)
                    .OrderBy(x => x.Sequence).ToArray();

                p.StartDateTime = contiguousPeriods.Any() ? contiguousPeriods.First().From : DateTime.MinValue;
                p.EndDateTime = contiguousPeriods.Any() ? contiguousPeriods.Last().To : DateTime.MaxValue;
            });

            map.Map<ValidityPeriodClosedEvent>().AsUpdateOf(e => e.DocumentNumber).Using((p, e, ctx) =>
            {
                var period = p.GetOrAddPeriod(e.Sequence);
                period.Status = "Closed";
                period.To = e.ClosedAt;
            });

            map.Map<ValidityPeriodCanceledEvent>().AsUpdateOf(e => e.DocumentNumber).Using((p, e, ctx) =>
            {
                var period = p.GetOrAddPeriod(e.Sequence);
                period.Status = "Canceled";
            });

            return map;
        }

        private static IEnumerable<ValidityPeriod> GetPreviousContiguousValidPeriods(List<ValidityPeriod> allPeriods,
            ValidityPeriod period)
        {
            while (period != null)
            {
                yield return period;

                ValidityPeriod previousPeriod =
                    allPeriods.SingleOrDefault(p => p.Status == "Valid" && p.To.Equals(period.From) && p.Sequence != period.Sequence);

                period = previousPeriod;
            }
        }
    }
}
