namespace FluidCaching
{
    public delegate TKey GetKey<T, TKey>(T item) where T : class;
}