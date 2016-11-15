using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using Chill;

using FluentAssertions;

using LiquidProjections.Logging;

using Xunit;

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
                    UseThe(new FakeEventStore());
                    WithSubject(_ => new Dispatcher(The<FakeEventStore>()));
                    LogProvider.SetCurrentLogProvider(UseThe(new FakeLogProvider()));
                    UseThe(new ProjectionException("Some message."));

                    // Use async throw to easily get a faulted task.
#pragma warning disable 1998
                    Subject.Subscribe(null, async transaction =>
                    {
                        throw The<ProjectionException>();
                    });
#pragma warning restore 1998
                });

                When(() => The<FakeEventStore>().Handler(null));
            }

            [Fact]
            public void It_should_unsubscribe()
            {
                The<FakeEventStore>().IsSubscribed.Should().BeFalse();
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

        internal class FakeEventStore : IEventStore
        {
            private readonly object syncRoot = new object();

            public bool IsSubscribed { get; private set; }
            public Func<IReadOnlyList<Transaction>, Task> Handler { get; private set; }

            public IDisposable Subscribe(long? checkpoint, Func<IReadOnlyList<Transaction>, Task> handler)
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