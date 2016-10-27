namespace LiquidProjections.NHibernate
{
    public interface IHaveIdentity<TKey>
    {
        TKey Id { get; set; }
    }
}