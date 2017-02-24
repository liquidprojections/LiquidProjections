using System.Collections.Generic;

namespace LiquidProjections.NEventStore
{
    internal sealed class Page
    {
        public Page(long previousCheckpoint, IReadOnlyList<Transaction> transactions)
        {
            PreviousCheckpoint = previousCheckpoint;
            Transactions = transactions;
        }

        public long PreviousCheckpoint { get; }

        public IReadOnlyList<Transaction> Transactions { get; }

        public long LastCheckpoint => Transactions.Count == 0 ? 0 : Transactions[Transactions.Count - 1].Checkpoint;
    }
}