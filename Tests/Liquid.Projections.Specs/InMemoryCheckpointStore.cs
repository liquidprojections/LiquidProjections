using System.Threading.Tasks;

namespace eVision.QueryHost.Specs
{
    public class InMemoryCheckpointStore : ICheckpointStore
    {
        private string checkpointToken;

        public InMemoryCheckpointStore(string checkpointToken = null)
        {
            this.checkpointToken = checkpointToken;
        }

        public Task<string> Get()
        {
            return Task.FromResult(checkpointToken);
        }

        public Task Put(string checkpointToken)
        {
            this.checkpointToken = checkpointToken;
            return Task.FromResult(0);
        }
    }
}