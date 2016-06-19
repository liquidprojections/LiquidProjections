using System;
using System.Threading.Tasks;
using Chill;

namespace eVision.QueryHost.Raven.Specs
{
    public static class GivenSubjectExtensions
    {
        public static void Given<TSubject>(this GivenSubject<TSubject> self, Func<Task> asyncAction)
            where TSubject : class
        {
            self.Given(() => asyncAction().Wait());
        }
    }
}