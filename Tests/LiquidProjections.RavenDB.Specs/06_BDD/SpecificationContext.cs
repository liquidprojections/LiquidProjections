using System.Threading.Tasks;

namespace LiquidProjections.RavenDB.Specs._06_BDD
{
    public abstract class SpecificationContext
    {
        protected SpecificationContext()
        {
            Task.Run(async () =>
            {
                await EstablishContext();
                await Because();
            }).Wait();
        }

        protected virtual Task Because()
        {
            return Task.FromResult(0);
        }

        protected virtual Task EstablishContext()
        {
            return Task.FromResult(0);
        }
    }
}