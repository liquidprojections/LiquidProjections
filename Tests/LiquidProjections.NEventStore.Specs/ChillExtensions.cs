using System;
using System.Threading.Tasks;
using Chill;

namespace LiquidProjections.NEventStore.Specs
{
    internal static class ChillExtensions
    {
        public static void GivenAsync<T>(this GivenSubject<T> given, Func<Task> givenFuncAsync) where T : class
        {
            given.Given(() => Task.Run(givenFuncAsync).Wait());
        }
        public static void WhenAsync<T>(this GivenSubject<T> given, Func<Task> givenFuncAsync) where T : class
        {
            given.When(() => Task.Run(givenFuncAsync).Wait());
        }
    }
}