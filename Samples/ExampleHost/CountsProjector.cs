using System;
using System.Collections.Generic;
using System.Linq;
using LiquidProjections.ExampleHost.Events;
using LiquidProjections.Statistics;

namespace LiquidProjections.ExampleHost
{
    public class CountsProjector
    {
        private readonly Dispatcher dispatcher;
        private readonly InMemoryDatabase store;
        private readonly ProjectionStats stats;
        private ExampleProjector<DocumentCountProjection> documentCountProjector;
        private ExampleProjector<CountryLookup> countryLookupProjector;

        public CountsProjector(Dispatcher dispatcher, InMemoryDatabase store, ProjectionStats stats)
        {
            this.dispatcher = dispatcher;
            this.store = store;
            this.stats = stats;

            BuildCountryProjector();
            BuildDocumentProjector();
        }

        public void Start()
        {
            dispatcher.Subscribe(null, async (transactions, info) =>
            {
                await documentCountProjector.Handle(transactions);
            });
        }

        private void BuildDocumentProjector()
        {
            var documentMapBuilder = new EventMapBuilder<DocumentCountProjection, string, ProjectionContext>();

            documentMapBuilder
                .Map<WarrantAssignedEvent>()
                .AsCreateOf(anEvent => anEvent.Number)
                .Using((document, anEvent) =>
                {
                    document.Type = "Warrant";
                    document.Kind = anEvent.Kind;
                    document.Country = anEvent.Country;
                    document.CountryName = GetCountryName(anEvent.Country);
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
                    document.CountryName = GetCountryName(anEvent.Country);
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
                    document.CountryName = GetCountryName(anEvent.Country);
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
                    document.CountryName = GetCountryName(anEvent.Country);
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
                    document.CountryName = GetCountryName(anEvent.Country);
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
                    document.CountryName = GetCountryName(anEvent.Country);
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
                .Using((document, anEvent) =>
                {
                    document.Country = anEvent.Country;
                    document.CountryName = GetCountryName(anEvent.Country);
                });

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

            documentCountProjector =
                new ExampleProjector<DocumentCountProjection>(documentMapBuilder, store, stats, countryLookupProjector)
                {
                    Id = "DocumentCount"
                };
        }

        private string GetCountryName(Guid countryCode)
        {
            var lookup = store.GetRepository<CountryLookup>().Find(countryCode.ToString());
            return (lookup != null) ? lookup.Name : "";
        }

        private void BuildCountryProjector()
        {
            var countryMapBuilder = new EventMapBuilder<CountryLookup, string, ProjectionContext>();

            countryMapBuilder
                .Map<CountryRegisteredEvent>()
                .AsCreateOf(anEvent => anEvent.Code)
                .Using((country, anEvent) => country.Name = anEvent.Name);

            countryLookupProjector = new ExampleProjector<CountryLookup>(countryMapBuilder, store, stats)
            {
                Id = "CountryLookup"
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
