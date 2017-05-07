using System;
using System.Collections.Generic;
using System.Linq;

namespace LiquidProjections.ExampleHost
{
    /// <summary>
    /// Sample database that uses an internal list and which is NOT thread-safe.
    /// </summary>
    public class InMemoryDatabase
    {
        private readonly List<IEntity> committed = new List<IEntity>();
        private readonly List<IEntity> uncommittedInserts = new List<IEntity>();
        private readonly List<IEntity> uncommittedDeletes = new List<IEntity>();

        /// <summary>
        /// Gets a value indicating whether this data mapper is currently maintaining a transaction.
        /// </summary>
        /// <value>
        /// <c>true</c> if this instance has transaction; otherwise, <c>false</c>.
        /// </value>
        public bool HasTransaction { get; set; }

        /// <summary>
        /// Gets a value indicating whether the data mapper still has changes to insert or delete.
        /// </summary>
        public bool HasChanges => uncommittedInserts.Any() || uncommittedDeletes.Any();

        public bool Saved { get; private set; }

        public bool IsDisposed { get; private set; }

        public IEnumerable<T> GetCommittedEntities<T>() where T : class, IEntity
        {
            return committed.OfType<T>();
        }

        public IEnumerable<T> GetUncommittedEntities<T>() where T : class, IEntity
        {
            return uncommittedInserts.OfType<T>();
        }

        public IEnumerable<T> GetUncommittedDeletes<T>() where T : class, IEntity
        {
            return uncommittedDeletes.OfType<T>();
        }

        public void AddCommitted(params IEntity[] entities)
        {
            foreach (var entity in entities)
            {
                if (!committed.Contains(entity))
                {
                    committed.Add(entity);
                }
            }
        }

        /// <summary>
        /// Gets a repository object for persisting and loading persistable objects.
        /// </summary>
        public Repository<T> GetRepository<T>() where T : class, IEntity
        {
            return new InMemoryRepository<T>(this);
        }

        public void Add(IEntity aggregate)
        {
            if (!uncommittedInserts.Contains(aggregate))
            {
                uncommittedInserts.Add(aggregate);
            }
        }

        public IEntity Get(Type type, string key)
        {
            IEntity entity = Find(type, key);
            if (entity == null)
            {
                throw new ApplicationException($"{type} with key {key} not found");
            }

            return entity;
        }

        public IEntity Find(Type aggregateRootType, string key)
        {
            return committed
                .Union(uncommittedInserts)
                .SingleOrDefault(c => aggregateRootType.IsInstanceOfType(c) && ((IEntity) c).Id.Equals(key));
        }

        public bool Exists(Type aggregateRootType, string key)
        {
            return committed
                .Union(uncommittedInserts)
                .Any(c => aggregateRootType.IsInstanceOfType(c) && ((IEntity) c).Id.Equals(key));
        }

        public void SubmitChanges()
        {
            committed.AddRange(uncommittedInserts);
            uncommittedInserts.Clear();
            committed.RemoveAll(e => uncommittedDeletes.Contains(e));
            uncommittedDeletes.Clear();
            Saved = true;
        }

        /// <summary>
        /// Removes all entities from the current unit of work.
        /// </summary>
        public void EvictAll()
        {
            committed.Clear();
            uncommittedDeletes.Clear();
            uncommittedInserts.Clear();
        }

        /// <summary>
        /// Removes an individual entity from the current unit of work.
        /// </summary>
        public void Evict(IEntity aggregate)
        {
            committed.Remove(aggregate);
            uncommittedDeletes.Remove(aggregate);
            uncommittedInserts.Remove(aggregate);
        }

        protected IEnumerable<object> Entities => committed.Union(uncommittedInserts).ToArray();

        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        /// <filterpriority>2</filterpriority>
        public void Dispose()
        {
            IsDisposed = true;
        }

        private sealed class InMemoryRepository<T> : Repository<T> where T : class, IEntity
        {
            private readonly InMemoryDatabase parent;

            public InMemoryRepository(InMemoryDatabase parent)
            {
                this.parent = parent;
            }

            protected override IQueryable<T> Entities
            {
                get { return parent.Entities.OfType<T>().AsQueryable(); }
            }

            public override T Find(string identity)
            {
                return (T)parent.Find(typeof(T), identity);
            }

            protected override void AddEntity(T entity)
            {
                parent.uncommittedInserts.Add(entity);
            }

            protected override void RemoveEntity(T entity)
            {
                parent.committed.Remove(entity);
                parent.uncommittedInserts.Remove(entity);

                parent.uncommittedDeletes.Add(entity);
            }
        }
    }
}