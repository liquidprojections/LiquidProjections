namespace FluidCaching
{
    /// <summary>
    /// This interface exposes the public part of a LifespanMgr.Node
    /// </summary>
    internal interface INode<T> where T : class
    {
        T Value { get; }
        void Touch();
        void Remove();
    }
}