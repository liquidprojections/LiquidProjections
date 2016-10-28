using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Runtime.Serialization.Formatters;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace LiquidProjections.ExampleHost
{
    public class JsonFileEventStore : IEventStore, IDisposable
    {
        private const int AverageEventsPerTransaction = 6;
        private readonly int pageSize;
        private ZipArchive zip;
        private readonly Queue<ZipArchiveEntry> entryQueue;
        private StreamReader currentReader = null;
        private static long lastCheckpoint = 0;

        public JsonFileEventStore(string filePath, int pageSize)
        {
            this.pageSize = pageSize;
            zip = ZipFile.Open(filePath, ZipArchiveMode.Read);
            entryQueue = new Queue<ZipArchiveEntry>(zip.Entries.Where(e => e.Name.EndsWith(".json")));
        }

        public IDisposable Subscribe(long? checkpoint, Func<IReadOnlyList<Transaction>, Task> handler)
        {
            var subscriber = new Subscriber(checkpoint ?? 0, handler);
            
            Task.Run(async () =>
            {
                Task<Transaction[]> loader = LoadNextPageAsync();
                Transaction[] transactions = await loader;

                while (transactions.Length > 0)
                {
                    // Start loading the next page on a separate thread while we have the subscriber handle the previous transactions.
                    loader = LoadNextPageAsync();

                    await subscriber.Send(transactions);

                    transactions = await loader;
                }
            });

            return subscriber;
        }

        private Task<Transaction[]> LoadNextPageAsync()
        {
            return Task.Run(() =>
            {
                var transactions = new List<Transaction>();

                var transaction = new Transaction
                {
                    Checkpoint = ++lastCheckpoint
                };

                string json;

                do
                {
                    json = CurrentReader.ReadLine();

                    if (json != null)
                    {
                        transaction.Events.Add(new EventEnvelope
                        {
                            Body = JsonConvert.DeserializeObject(json, new JsonSerializerSettings
                            {
                                TypeNameHandling = TypeNameHandling.All,
                                TypeNameAssemblyFormat = FormatterAssemblyStyle.Full
                            })
                        });
                    }

                    if ((transaction.Events.Count == AverageEventsPerTransaction) || (json == null))
                    {
                        if (transaction.Events.Count > 0)
                        {
                            transactions.Add(transaction);
                        }

                        transaction = new Transaction
                        {
                            Checkpoint = ++lastCheckpoint
                        };
                    }
                }
                while ((json != null) && (transactions.Count < pageSize));

                return transactions.ToArray();
            });
        }

        private StreamReader CurrentReader => 
            currentReader ?? (currentReader = new StreamReader(entryQueue.Dequeue().Open()));

        public void Dispose()
        {
            zip.Dispose();
            zip = null;
        }

        internal class Subscriber : IDisposable
        {
            private readonly long fromCheckpoint;
            private readonly Func<IReadOnlyList<Transaction>, Task> handler;
            private bool disposed;

            public Subscriber(long fromCheckpoint, Func<IReadOnlyList<Transaction>, Task> handler)
            {
                this.fromCheckpoint = fromCheckpoint;
                this.handler = handler;
            }

            public async Task Send(IEnumerable<Transaction> transactions)
            {
                if (!disposed)
                {
                    Transaction[] readOnlyList = transactions.Where(t => t.Checkpoint >= fromCheckpoint).ToArray();
                    if (readOnlyList.Length > 0)
                    {
                        await handler(readOnlyList);
                    }
                }
                else
                {
                    throw new ObjectDisposedException("");
                }
            }

            public void Dispose()
            {
                disposed = true;
            }
        }
    }
}