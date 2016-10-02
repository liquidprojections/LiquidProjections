using System;
using System.Linq;
using Raven.Client.Indexes;

namespace LiquidProjections.ExampleHost
{
    public class Documents_ByDynamicState :
        AbstractIndexCreationTask<DocumentCountProjection, Documents_ByDynamicState.Result>
    {
        public class Result
        {
            public Guid Country { get; set; }

            public string CountryName { get; set; }

            public string Kind { get; set; }
        }

        public Documents_ByDynamicState()
        {
            Map = documents =>
                from document in documents
                let dynamicStates = new[] { "Active" }
                where dynamicStates.Contains(document.State)
                select new Result
                {
                    Country = document.Country,
                    CountryName = LoadDocument<CountryLookup>(document.Country.ToString()).Name,
                    Kind = document.Kind
                };
        }
    }
}