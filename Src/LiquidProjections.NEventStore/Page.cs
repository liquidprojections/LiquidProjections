using System.Collections.Generic;

namespace LiquidProjections.NEventStore
{
    internal sealed class Page
    {
        public Page(string previousCheckpoint, IReadOnlyList<Transaction> transactions)
        {
            PreviousCheckpoint = previousCheckpoint;
            Transactions = transactions;
        }

        public string PreviousCheckpoint { get; }
        public IReadOnlyList<Transaction> Transactions { get; }

        public string LastCheckpoint => (Transactions.Count == 0) ? null : Transactions[Transactions.Count - 1].Checkpoint;
    }
}