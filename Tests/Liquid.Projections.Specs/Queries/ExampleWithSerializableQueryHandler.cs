using System;
using System.Linq;
using System.Threading.Tasks;

namespace eVision.QueryHost.Specs.Queries
{
    public class ExampleWithSerializableQueryHandler :
        IQueryHandler<ExampleWithSerializableQuery, ExampleWithSerializableQuery.Result>,
        IQueryHandler<ExampleWithSerializableInterfaceQuery, ExampleWithSerializableInterfaceQuery.Result>
    {
        public Task<ExampleWithSerializableQuery.Result> Handle(ExampleWithSerializableQuery query)
        {
            return Task.FromResult(new ExampleWithSerializableQuery.Result
            {
                ConvertedValues = query.Values.Select(v => v.ToUpper()).ToList(),
                Count = query.Values.Count
            });
        }

        public Task<ExampleWithSerializableInterfaceQuery.Result> Handle(ExampleWithSerializableInterfaceQuery query)
        {
            var values =
                Enumerable.Range(1, query.Values.Count)
                    .Select(_ => Tuple.Create(new SiteCode(Guid.NewGuid()), query.Values.ToArray()))
                    .ToDictionary(x => x.Item1, x => x.Item2);

            return Task.FromResult(new ExampleWithSerializableInterfaceQuery.Result
            {
                ConvertedValues = new ComplexKeySerializableDictionary<SiteCode, string[]>(values),
                Count = query.Values.Count
            });
        }
    }
}