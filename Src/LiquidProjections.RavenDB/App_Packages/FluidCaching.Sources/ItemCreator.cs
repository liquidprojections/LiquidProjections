using System.Threading.Tasks;

namespace FluidCaching
{
    public delegate Task<T> ItemCreator<in TKey, T>(TKey key) where T : class;
}