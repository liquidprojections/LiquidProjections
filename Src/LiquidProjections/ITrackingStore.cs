using System.Threading.Tasks;

namespace LiquidProjections
{
    public interface ITrackingStore
    {
        Task<long?> LoadCheckpoint(string projectorId);

        Task SaveCheckpoint(string projectorId, long checkpoint);
    }
}