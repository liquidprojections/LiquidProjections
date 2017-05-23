using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using Chill;

using FluentAssertions;
using LiquidProjections.Abstractions;
using LiquidProjections.Logging;
using LiquidProjections.Testing;
using Xunit;
// ReSharper disable ConvertToLambdaExpression

namespace LiquidProjections.Specs
{
    namespace DispatcherSpecs
    {
        public class When_a_projector_throws_an_exception : GivenSubject<Dispatcher>
        {
            public When_a_projector_throws_an_exception()
            {
                Given(() =>
                {
                    UseThe(new MemoryEventSource());
                    WithSubject(_ => new Dispatcher(The<MemoryEventSource>().Subscribe));

                    LogProvider.SetCurrentLogProvider(UseThe(new FakeLogProvider()));

                    UseThe(new ProjectionException("Some message."));

                    Subject.Subscribe(null, (transaction, info) =>
                    {
                        // Use async throw to easily get a faulted task.
                        throw The<ProjectionException>();
                    }, 
                    new SubscriptionOptions
                    {
                        Id = "mySubscription"
                    });
                });

                When(() =>
                {
                    return The<MemoryEventSource>().Write(new List<Transaction>());
                });
            }

            [Fact]
            public void It_should_unsubscribe()
            {
                The<MemoryEventSource>().HasSubscriptionForId("mySubscription").Should().BeFalse();
            }

            [Fact]
            public void It_should_log_fatal_error()
            {
                The<FakeLogProvider>().LogLevel.Should().Be(LogLevel.Fatal);
            }

            [Fact]
            public void It_should_include_the_exception()
            {
                The<FakeLogProvider>().Exception.Should().Be(The<ProjectionException>());
            }
        }

        public class When_the_requested_checkpoint_is_ahead_of_the_store_and_auto_restart_is_configured : GivenSubject<Dispatcher>
        {
            private readonly BlockingCollection<string> trace = new BlockingCollection<string>();

            private readonly BlockingCollection<Transaction> receivedTransactions = new BlockingCollection<Transaction>();

            private readonly TaskCompletionSource<IEnumerable<Transaction>> allTransactionsReceived = 
                new TaskCompletionSource<IEnumerable<Transaction>>();

            public When_the_requested_checkpoint_is_ahead_of_the_store_and_auto_restart_is_configured()
            {
                Given(async () =>
                {
                    UseThe(new MemoryEventSource());

                    WithSubject(_ => new Dispatcher(The<MemoryEventSource>().Subscribe));

                    await The<MemoryEventSource>().Write(
                        new TransactionBuilder().WithCheckpoint(1).Build());

                    await The<MemoryEventSource>().Write(
                        new TransactionBuilder().WithCheckpoint(999).Build());
                });

                When(() =>
                {
                    var options = new SubscriptionOptions
                    {
                        Id = "someId",
                        RestartWhenAhead = true,
                        BeforeRestarting = () =>
                        {
                            trace.Add("BeforeRestarting");
                            return Task.FromResult(0);
                        }
                    };

                    Subject.Subscribe(1000, (transactions, info) =>
                    {
                        trace.Add("TransactionsReceived");

                        foreach (var transaction in transactions)
                        {
                            receivedTransactions.Add(transaction);
                        }

                        if (receivedTransactions.Count == 2)
                        {
                            allTransactionsReceived.SetResult(transactions);
                        }

                        return Task.FromResult(0);

                    }, options);
                });
            }

            [Fact]
            public async Task It_should_allow_the_subscriber_to_cleanup_before_restarting()
            {
                await allTransactionsReceived.Task.TimeoutAfter(30.Seconds());

                trace.Should().Equal("BeforeRestarting", "TransactionsReceived", "TransactionsReceived");
            }

            [Fact]
            public async Task It_should_restart_sending_transactions_from_the_beginning()
            {
                var transactions = await allTransactionsReceived.Task.TimeoutAfter(30.Seconds());

                transactions.First().Checkpoint.Should().Be(999);
            }
        }
        public class When_there_are_no_new_transactions_available_and_auto_restart_is_configured : GivenSubject<Dispatcher>
        {
            private readonly BlockingCollection<Transaction> receivedTransactions = new BlockingCollection<Transaction>();

            private readonly TaskCompletionSource<bool> allTransactionsReceived = 
                new TaskCompletionSource<bool>();

            private bool restarted = false;

            public When_there_are_no_new_transactions_available_and_auto_restart_is_configured()
            {
                Given(async () =>
                {
                    UseThe(new MemoryEventSource());

                    WithSubject(_ => new Dispatcher(The<MemoryEventSource>().Subscribe));

                    await The<MemoryEventSource>().Write(
                        new TransactionBuilder().WithCheckpoint(123).Build());

                    await The<MemoryEventSource>().Write(
                        new TransactionBuilder().WithCheckpoint(456).Build());
                });

                When(() =>
                {
                    var options = new SubscriptionOptions
                    {
                        Id = "someId",
                        RestartWhenAhead = true,
                        BeforeRestarting = () =>
                        {
                            restarted = true;
                            return Task.FromResult(0);
                        }
                    };

                    Subject.Subscribe(123, (transactions, info) =>
                    {
                        foreach (var transaction in transactions)
                        {
                            receivedTransactions.Add(transaction);
                        }

                        if (receivedTransactions.Count == 1)
                        {
                            allTransactionsReceived.SetResult(true);
                        }

                        return Task.FromResult(0);

                    }, options);
                });
            }

            [Fact]
            public async Task It_should_only_provide_the_newer_transactions()
            {
                await allTransactionsReceived.Task.TimeoutAfter(30.Seconds());

                receivedTransactions.Should().HaveCount(1);
            }

            public async Task It_should_not_have_restartedAsync()
            {
                await allTransactionsReceived.Task.TimeoutAfter(30.Seconds());

                restarted.Should().BeFalse();
            }
        }

        internal class FakeEventStore
        {
            private readonly object syncRoot = new object();

            public bool IsSubscribed { get; private set; }
            public Func<IReadOnlyList<Transaction>, SubscriptionInfo, Task> Handler { get; private set; }

            public IDisposable Subscribe(long? previousCheckpoint, Func<IReadOnlyList<Transaction>, SubscriptionInfo,Task> handler, string subscriptionId = null)
            {
                lock (syncRoot)
                {
                    IsSubscribed = true;
                }

                Handler = handler;

                return new Subscription(this);
            }

            private class Subscription : IDisposable
            {
                private readonly FakeEventStore eventStore;

                public Subscription(FakeEventStore eventStore)
                {
                    this.eventStore = eventStore;
                }

                public void Dispose()
                {
                    lock (eventStore.syncRoot)
                    {
                        eventStore.IsSubscribed = false;
                    }
                }
            }
        }

        internal class FakeLogProvider : ILogProvider
        {
            public LogLevel? LogLevel { get; private set; }
            public string Message { get; private set; }
            public Exception Exception { get; private set; }

            public Logger GetLogger(string name)
            {
                return (logLevel, getMessage, exception, formatParameters) =>
                {
                    LogLevel = logLevel;

                    if (getMessage == null)
                    {
                        Message = null;
                    }
                    else
                    {
                        if (formatParameters?.Any() ?? false)
                        {
                            Message = string.Format(getMessage(), formatParameters);
                        }
                        else
                        {
                            Message = getMessage();
                        }
                    }

                    Exception = exception;
                    return true;
                };
            }

            public IDisposable OpenNestedContext(string message)
            {
                throw new NotImplementedException();
            }

            public IDisposable OpenMappedContext(string key, string value)
            {
                throw new NotImplementedException();
            }
        }

        internal class SomeEvent
        {
            public int X { get; set; }
        }
    }
}