namespace FluidCaching
{
    /// <summary>
    /// Because there is no auto inheritance between generic types, this interface is used to send messages to Index objects
    /// </summary>
    internal interface IIndexManagement<T> where T : class
    {
        void ClearIndex();
        bool AddItem(INode<T> item);
        INode<T> FindItem(T item);
        int RebuildIndex();
    }
}