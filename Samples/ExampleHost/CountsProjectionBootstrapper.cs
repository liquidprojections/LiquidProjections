using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
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
        private readonly Stopwatch stopwatch = new Stopwatch();
        private long eventCount = 0;
        private long transactionCount = 0;
        private RavenProjector<DocumentCountProjection> documentProjector;
        private RavenChildProjector<CountryLookup> countryProjector;
        private readonly LruProjectionCache cache;

        public CountsProjectionBootstrapper(Dispatcher dispatcher, Func<IAsyncDocumentSession> sessionFactory)
        {
            this.dispatcher = dispatcher;
            this.sessionFactory = sessionFactory;
            cache = new LruProjectionCache(20000, TimeSpan.FromSeconds(30), TimeSpan.FromMinutes(2), () => DateTime.UtcNow);

            BuildCountryProjector();
            BuildDocumentProjector();
        }

        public async Task Start()
        {
            long lastCheckpoint = await documentProjector.GetLastCheckpoint() ?? 0;

            stopwatch.Start();

            dispatcher.Subscribe(lastCheckpoint, async transactions =>
            {
                await documentProjector.Handle(transactions);

                transactionCount += transactions.Count;
                eventCount += transactions.Sum(t => t.Events.Count);

                long elapsedTotalSeconds = (long)stopwatch.Elapsed.TotalSeconds;

                if ((transactionCount % 100 == 0) && (elapsedTotalSeconds > 0))
                {
                    int ratePerSecond = (int)(eventCount / elapsedTotalSeconds);

                    Console.WriteLine($"{DateTime.Now}: Processed {eventCount} events " +
                        $"(rate: {ratePerSecond}/second, hits: {cache.Hits}, Misses: {cache.Misses})");
                }
            });
        }

        private void BuildDocumentProjector()
        {
            var documentMapBuilder = new EventMapBuilder<DocumentCountProjection, string, RavenProjectionContext>();

            documentMapBuilder
                .Map<WarrantAssignedEvent>()
                .AsCreateOf(anEvent => anEvent.Number)
                .Using((document, anEvent) =>
                {
                    document.Type = "Warrant";
                    document.Kind = anEvent.Kind;
                    document.Country = anEvent.Country;
                    document.State = anEvent.InitialState;
                });

            documentMapBuilder
                .Map<CertificateIssuedEvent>()
                .AsCreateOf(anEvent => anEvent.Number)
                .Using((document, anEvent) =>
                {
                    document.Type = "Certificate";
                    document.Kind = anEvent.Kind;
                    document.Country = anEvent.Country;
                    document.State = anEvent.InitialState;
                });

            documentMapBuilder
                .Map<ConstitutionEstablishedEvent>()
                .AsUpdateOf(anEvent => anEvent.Number)
                .Using((document, anEvent) =>
                {
                    document.Type = "Constitution";
                    document.Kind = anEvent.Kind;
                    document.Country = anEvent.Country;
                    document.State = anEvent.InitialState;
                });

            documentMapBuilder
                .Map<LicenseGrantedEvent>()
                .AsCreateOf(anEvent => anEvent.Number)
                .Using((document, anEvent) =>
                {
                    document.Type = "Audit";
                    document.Kind = anEvent.Kind;
                    document.Country = anEvent.Country;
                    document.State = anEvent.InitialState;
                });

            documentMapBuilder
                .Map<ContractNegotiatedEvent>()
                .AsCreateOf(anEvent => anEvent.Number)
                .Using((document, anEvent) =>
                {
                    document.Type = "Task";
                    document.Kind = anEvent.Kind;
                    document.Country = anEvent.Country;
                    document.State = anEvent.InitialState;
                });

            documentMapBuilder
                .Map<BondIssuedEvent>()
                .AsCreateOf(anEvent => anEvent.Number)
                .Using((document, anEvent) =>
                {
                    document.Type = "IsolationCertificate";
                    document.Kind = anEvent.Kind;
                    document.Country = anEvent.Country;
                    document.State = anEvent.InitialState;
                });

            documentMapBuilder
                .Map<AreaRestrictedEvent>()
                .AsUpdateOf(anEvent => anEvent.DocumentNumber)
                .Using((document, anEvent) => document.RestrictedArea = anEvent.Area);

            documentMapBuilder
                .Map<AreaRestrictionCancelledEvent>()
                .AsUpdateOf(anEvent => anEvent.DocumentNumber)
                .Using((document, anEvent) => document.RestrictedArea = null);

            documentMapBuilder
                .Map<StateTransitionedEvent>()
                .When(anEvent => anEvent.State != "Closed")
                .AsUpdateOf(anEvent => anEvent.DocumentNumber)
                .Using((document, anEvent) => document.State = anEvent.State);

            documentMapBuilder
                .Map<StateRevertedEvent>()
                .AsUpdateOf(anEvent => anEvent.DocumentNumber)
                .Using((document, anEvent) => document.State = anEvent.State);

            documentMapBuilder
                .Map<DocumentArchivedEvent>()
                .AsDeleteOf(anEvent => anEvent.DocumentNumber);

            documentMapBuilder
                .Map<CountryCorrectedEvent>()
                .AsUpdateOf(anEvent => anEvent.DocumentNumber)
                .Using((document, anEvent) => document.Country = anEvent.Country);

            documentMapBuilder
                .Map<NextReviewScheduledEvent>()
                .AsUpdateOf(anEvent => anEvent.DocumentNumber)
                .Using((document, anEvent) => document.NextReviewAt = anEvent.NextReviewAt);

            documentMapBuilder
                .Map<LifetimeRestrictedEvent>()
                .AsUpdateOf(anEvent => anEvent.DocumentNumber)
                .Using((document, anEvent) => document.LifetimePeriodEnd = anEvent.PeriodEnd);

            documentMapBuilder
                .Map<LifetimeRestrictionRemovedEvent>()
                .AsUpdateOf(anEvent => anEvent.DocumentNumber)
                .Using((document, anEvent) => document.LifetimePeriodEnd = null);

            documentMapBuilder
                .Map<ValidityPeriodPlannedEvent>()
                .AsUpdateOf(anEvent => anEvent.DocumentNumber)
                .Using((document, anEvent) =>
                {
                    ValidityPeriod period = document.GetOrAddPeriod(anEvent.Sequence);
                    period.From = anEvent.From;
                    period.To = anEvent.To;
                });

            documentMapBuilder
                .Map<ValidityPeriodResetEvent>()
                .AsUpdateOf(anEvent => anEvent.DocumentNumber)
                .Using((document, anEvent) =>
                {
                    ValidityPeriod period = document.GetOrAddPeriod(anEvent.Sequence);
                    period.From = null;
                    period.To = null;
                });

            documentMapBuilder
                .Map<ValidityPeriodApprovedEvent>()
                .AsUpdateOf(anEvent => anEvent.DocumentNumber)
                .Using((document, anEvent) =>
                {
                    ValidityPeriod period = document.GetOrAddPeriod(anEvent.Sequence);
                    period.Status = "Valid";

                    ValidityPeriod lastValidPeriod = document.Periods.LastOrDefault(aPeriod => aPeriod.Status == "Valid");

                    ValidityPeriod[] contiguousPeriods = GetPreviousContiguousValidPeriods(document.Periods, lastValidPeriod)
                        .OrderBy(x => x.Sequence).ToArray();

                    document.StartDateTime = contiguousPeriods.Any() ? contiguousPeriods.First().From : DateTime.MinValue;
                    document.EndDateTime = contiguousPeriods.Any() ? contiguousPeriods.Last().To : DateTime.MaxValue;
                });

            documentMapBuilder
                .Map<ValidityPeriodClosedEvent>()
                .AsUpdateOf(anEvent => anEvent.DocumentNumber)
                .Using((document, anEvent) =>
                {
                    ValidityPeriod period = document.GetOrAddPeriod(anEvent.Sequence);
                    period.Status = "Closed";
                    period.To = anEvent.ClosedAt;
                });

            documentMapBuilder
                .Map<ValidityPeriodCanceledEvent>()
                .AsUpdateOf(anEvent => anEvent.DocumentNumber)
                .Using((document, anEvent) =>
                {
                    ValidityPeriod period = document.GetOrAddPeriod(anEvent.Sequence);
                    period.Status = "Canceled";
                });

            documentProjector = new RavenProjector<DocumentCountProjection>(
                sessionFactory,
                documentMapBuilder,
                new[] { countryProjector })
            {
                BatchSize = 20,
                Cache = cache
            };
        }

        private void BuildCountryProjector()
        {
            var countryMapBuilder = new EventMapBuilder<CountryLookup, string, RavenProjectionContext>();

            countryMapBuilder
                .Map<CountryRegisteredEvent>()
                .AsCreateOf(anEvent => anEvent.Code)
                .Using((country, anEvent) => country.Name = anEvent.Name);

            countryProjector = new RavenChildProjector<CountryLookup>(countryMapBuilder)
            {
                Cache = cache
            };
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
