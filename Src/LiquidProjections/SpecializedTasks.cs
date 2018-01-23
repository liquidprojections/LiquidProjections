using System.Threading.Tasks;

namespace LiquidProjections
{
    internal static class SpecializedTasks
    {
        internal static readonly Task<bool> FalseTask = Task.FromResult(false);
        internal static readonly Task<int> ZeroTask = Task.FromResult(0);
    }
}
