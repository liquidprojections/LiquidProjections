using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;

namespace LiquidProjections.ExampleHost
{
    public abstract class Repository<T> : IQueryable<T> where T : class, IEntity
    {
        protected abstract IQueryable<T> Entities { get; }

        /// <summary>
        /// Returns the total number of entities in the repository.
        /// </summary>
        public int Count => Entities.Count();

        public Type ElementType => Entities.ElementType;

        public Expression Expression => Entities.Expression;

        public IQueryProvider Provider => Entities.Provider;

        /// <summary>
        /// Returns the object identified by <paramref name="identity"/> or <c>null</c> if no such object exists.
        /// </summary>
        public abstract T Find(string identity);

        public void AddRange(IEnumerable<T> entities)
        {
            foreach (var entity in entities)
            {
                Add(entity);
            }
        }

        public void Add(T entity)
        {
            AddEntity(entity);
        }

        protected abstract void AddEntity(T entity);

        public void RemoveRange(IEnumerable<T> entities)
        {
            foreach (var entity in entities)
            {
                Remove(entity);
            }
        }

        public void RemoveByKey(string key)
        {
            T entity = Entities.SingleOrDefault(e => e.Id == key);
            if (entity != null)
            {
                RemoveEntity(entity);
            }
        }

        public void Remove(T entity)
        {
            RemoveEntity(entity);
        }

        public void Clear()
        {
            RemoveRange(Entities);
        }

        protected abstract void RemoveEntity(T entity);

        public IEnumerator<T> GetEnumerator()
        {
            return Entities.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return Entities.GetEnumerator();
        }


    }
}