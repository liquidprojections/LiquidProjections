using System;
using System.Collections.Generic;
using Chill;
using FluentAssertions;
using LiquidProjections.Testing;
using Xunit;

namespace LiquidProjections.Specs
{
    namespace MemoryEventSourceSpecs
    {
        public class When_a_subscriber_throws_an_exception_that_the_dispatcher_rethrows : GivenSubject<Dispatcher>
        {
            public When_a_subscriber_throws_an_exception_that_the_dispatcher_rethrows()
            {
                Given(() =>
                {
                    UseThe(new MemoryEventSource());
                    WithSubject(_ => new Dispatcher(The<MemoryEventSource>().Subscribe)
                    {
                        ExceptionHandler = (exception, attempts, info) => throw exception
                    });

                    Subject.Subscribe(null, (transaction, info) =>
                    {
                        throw new ArgumentException();
                    });
                });

                WhenLater(() =>
                {
                    return The<MemoryEventSource>().Write(new List<Transaction>
                    {
                        new Transaction()
                    });
                });
            }

            [Fact]
            public void Then_the_exception_should_bubble_up_through_the_memory_event_source()
            {
                WhenAction.Should().Throw<ArgumentException>();
            }
        }

    }
}