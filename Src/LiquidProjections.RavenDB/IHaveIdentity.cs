namespace LiquidProjections.RavenDB
{
    public interface IHaveIdentity<TKey>
    {
        TKey Id { get; set; }
    }
}