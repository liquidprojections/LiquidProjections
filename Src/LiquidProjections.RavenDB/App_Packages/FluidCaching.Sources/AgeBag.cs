using System;

namespace FluidCaching
{
    /// <summary>container class used to hold nodes added within a descrete timeframe</summary>
    internal class AgeBag<T> where T : class
    {
        public DateTime StartTime { get; set; }

        public DateTime StopTime { get; set; }

        public Node<T> First { get; set; }

        public bool HasExpired(TimeSpan maxAge, DateTime now)
        {
            DateTime expirationPoint = now.Subtract(maxAge);

            return StartTime < expirationPoint;
        }

        public bool HasReachedMinimumAge(TimeSpan minAge, DateTime now)
        {
            return (now - StopTime) > minAge;
        }
    }
}