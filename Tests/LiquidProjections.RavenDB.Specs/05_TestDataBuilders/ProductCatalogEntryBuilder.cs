using System;

namespace LiquidProjections.RavenDB.Specs._05_TestDataBuilders
{
    internal class ProductCatalogEntryBuilder
    {
        private string id = Guid.NewGuid().ToString();
        private string category = Guid.NewGuid().ToString();

        public ProductCatalogEntryBuilder IdentifiedBy(string id)
        {
            this.id = id;
            return this;
        }

        public ProductCatalogEntryBuilder CategorizedAs(string category)
        {
            this.category = category;
            return this;
        }

        public ProductCatalogEntry Build()
        {
            return new ProductCatalogEntry
            {
                Id = id,
                Category = category
            };
        }
    }
}