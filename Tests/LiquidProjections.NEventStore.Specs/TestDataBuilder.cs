namespace LiquidProjections.NEventStore.Specs
{
    /// <summary>
    /// Generic test data builder for building test data.
    /// </summary>
    /// <typeparam name="TSubject">The type of object to build.</typeparam>
    public abstract class TestDataBuilder<TSubject>
    {
        protected bool HasBuild { get; set; }

        /// <summary>
        /// Builds an instance of <typeparamref name="TSubject"/>
        /// </summary>
        public virtual TSubject Build()
        {
            OnPreBuild();

            TSubject subject = OnBuild();

            OnPostBuild(subject);

            HasBuild = true;

            return subject;
        }

        /// <summary>
        /// Is called before <see cref="OnBuild"/> to allow builders to initialize all fields that are not 
        /// yet properly initalized, or require specialized initialization.
        /// </summary>
        protected virtual void OnPreBuild()
        {
        }

        /// <summary>
        /// Is called after <see cref="OnBuild"/> to allow builders to initialize the subject's properties
        /// and fields that have not been initialized during constructor.
        /// </summary>
        protected virtual void OnPostBuild(TSubject subject)
        {
        }

        /// <summary>
        /// Called to create an instance of <typeparamref name="TSubject"/>.
        /// </summary>
        /// <remarks>
        /// Do not initialize the builder here. Use <see cref="OnPreBuild"/> instead.
        /// </remarks>
        protected abstract TSubject OnBuild();
    }
}