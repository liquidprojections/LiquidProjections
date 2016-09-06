using System.Threading.Tasks;

namespace LiquidProjections
{
    public interface ITrackingStore
    {
        Task SaveCheckpoint(string projectorId, long checkpoint);
    }
}