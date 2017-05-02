using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace LiquidProjections
{
    /// <summary>
    /// An exception describing an unrecoverable error in a projector.
    /// </summary>
    public class ProjectionException : Exception
    {
        private EventEnvelope currentEvent;
        private string projector;
        private string childProjector;
        private string transactionId;

        public ProjectionException(string message)
            : this(message, null)
        {
        }

        public ProjectionException(string message, Exception inner)
            : base(message, inner)
        {
        }

        public EventEnvelope CurrentEvent
        {
            get { return currentEvent; }
            set
            {
                if ((currentEvent != null) && (currentEvent != value))
                {
                    throw new InvalidOperationException($"{nameof(CurrentEvent)} is already set to a different value.");
                }

                currentEvent = value;
            }
        }

        public string Projector
        {
            get { return projector; }
            set
            {
                if (!string.IsNullOrEmpty(projector) && (projector != value))
                {
                    throw new InvalidOperationException($"{nameof(Projector)} is already set to a different value.");
                }

                projector = value;
            }
        }

        public string ChildProjector
        {
            get { return childProjector; }
            set
            {
                if (!string.IsNullOrEmpty(childProjector) && (childProjector != value))
                {
                    throw new InvalidOperationException($"{nameof(ChildProjector)} is already set to a different value.");
                }

                childProjector = value;
            }
        }

        public string TransactionId
        {
            get { return transactionId; }
            set
            {
                if (!string.IsNullOrEmpty(transactionId) && (transactionId != value))
                {
                    throw new InvalidOperationException($"{nameof(TransactionId)} is already set to a different value.");
                }

                transactionId = value;
            }
        }

        public IReadOnlyList<Transaction> TransactionBatch { get; private set; }

        public void SetTransactionBatch(IEnumerable<Transaction> transactionBatch)
        {
            if (transactionBatch == null)
            {
                throw new ArgumentNullException(nameof(transactionBatch));
            }

            if (TransactionBatch != null)
            {
                throw new InvalidOperationException($"{nameof(TransactionBatch)} is already set.");
            }

            List<Transaction> transactionBatchCopy = transactionBatch.ToList();

            if (transactionBatchCopy.Contains(null))
            {
                throw new ArgumentException("Transaction batch contains null transaction.", nameof(transactionBatch));
            }

            TransactionBatch = new ReadOnlyCollection<Transaction>(transactionBatch.ToList());
        }
    }
}