using System;
using System.Threading.Tasks;
using Chill;

namespace LiquidProjections.RavenDB.Specs
{
    internal static class ChillExtensions
    {
        public static void GivenAsync(this GivenWhenThen given, Func<Task> givenFuncAsync)
        {
            given.Given(() => Task.Run(givenFuncAsync).Wait());
        }
        public static void WhenAsync(this GivenWhenThen given, Func<Task> givenFuncAsync)
        {
            given.When(() => Task.Run(givenFuncAsync).Wait());
        }
    }
}