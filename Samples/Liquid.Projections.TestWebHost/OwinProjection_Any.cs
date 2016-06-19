using System.Linq;
using Raven.Client.Indexes;

namespace QueryHost.TestWebHost
{
    public class OwinProjection_Any : AbstractIndexCreationTask<OwinProjection>
    {
        public OwinProjection_Any()
        {
            Map = docs => 
                from doc in docs 
                select new {};
        }
    }
}