#region

using System.Threading;
using System.Threading.Tasks;

#endregion

namespace eVision.QueryHost.Dispatching
{
    public interface IDispatchCommits
    {
        Task Dispatch(Transaction transaction, CancellationToken cancellationToken);
    }
}