using System;
using FluentAssertions;
using LiquidProjections.Statistics;
using Xunit;

namespace LiquidProjections.Specs
{
    public class ProjectionStatsSpecs
    {
        [Fact]
        public void When_checking_in_multiple_times_for_a_projector_it_should_remember_the_last_only()
        {
            //-----------------------------------------------------------------------------------------------------------
            // Arrange
            //-----------------------------------------------------------------------------------------------------------
            DateTime nowUtc = 16.June(2017).At(15, 00).AsUtc();

            var stats = new ProjectionStats(() => nowUtc);
            
            //-----------------------------------------------------------------------------------------------------------
            // Act
            //-----------------------------------------------------------------------------------------------------------
            stats.TrackProgress("myProjector", 1000);

            nowUtc = nowUtc.Add(1.Hours());

            stats.TrackProgress("myProjector", 2000);
            
            //-----------------------------------------------------------------------------------------------------------
            // Assert
            //-----------------------------------------------------------------------------------------------------------
            var projectorStats = stats.GetForAllProjectors().Should().ContainSingle(s => s.ProjectorId == "myProjector").Subject;
            projectorStats.LastCheckpoint.Checkpoint.Should().Be(2000);
            projectorStats.LastCheckpoint.TimestampUtc.Should().Be(nowUtc);
        }

        [Fact]
        public void When_multiple_properties_are_registered_under_the_same_name_it_should_only_remember_the_last_one()
        {
            DateTime nowUtc = 16.June(2017).At(15, 00).AsUtc();

            var stats = new ProjectionStats(() => nowUtc);

            //-----------------------------------------------------------------------------------------------------------
            // Act
            //-----------------------------------------------------------------------------------------------------------
            stats.StoreProperty("myProjector", "theName", "aValue");

            nowUtc = nowUtc.Add(1.Hours());

            stats.StoreProperty("myProjector", "theName", "anotherValue");

            //-----------------------------------------------------------------------------------------------------------
            // Assert
            //-----------------------------------------------------------------------------------------------------------
            var projectorStats = stats.GetForAllProjectors().Should().ContainSingle(s => s.ProjectorId == "myProjector").Subject;

            projectorStats.GetProperties().Should().ContainKey("theName");
            projectorStats.GetProperties()["theName"].Should().BeEquivalentTo(new
            {
                Value = "anotherValue",
                TimestampUtc = nowUtc
            });
        }

        [Fact]
        public void When_multiple_properties_are_registered_under_different_names_it_should_remember_all()
        {
            DateTime nowUtc = 16.June(2017).At(15, 00).AsUtc();

            var stats = new ProjectionStats(() => nowUtc);

            //-----------------------------------------------------------------------------------------------------------
            // Act
            //-----------------------------------------------------------------------------------------------------------
            stats.StoreProperty("myProjector", "aName", "aValue");

            var firstUtc = nowUtc;
            nowUtc = nowUtc.Add(1.Hours());

            stats.StoreProperty("myProjector", "anotherName", "anotherValue");

            //-----------------------------------------------------------------------------------------------------------
            // Assert
            //-----------------------------------------------------------------------------------------------------------
            var projectorStats = stats.GetForAllProjectors().Should().ContainSingle(s => s.ProjectorId == "myProjector").Subject;

            projectorStats.GetProperties().Should().ContainKey("aName");
            projectorStats.GetProperties()["aName"].Should().BeEquivalentTo(new
            {
                Value = "aValue",
                TimestampUtc = firstUtc
            });

            projectorStats.GetProperties().Should().ContainKey("anotherName");
            projectorStats.GetProperties()["anotherName"].Should().BeEquivalentTo(new
            {
                Value = "anotherValue",
                TimestampUtc = nowUtc
            });
        }

        [Fact]
        public void When_multiple_events_are_registered_it_should_remember_their_timestamps()
        {
            //-----------------------------------------------------------------------------------------------------------
            // Arrange
            //-----------------------------------------------------------------------------------------------------------
            DateTime nowUtc = 16.June(2017).At(15, 00).AsUtc();

            var stats = new ProjectionStats(() => nowUtc);

            //-----------------------------------------------------------------------------------------------------------
            // Act
            //-----------------------------------------------------------------------------------------------------------
            stats.LogEvent("myProjector", "first event");

            nowUtc = nowUtc.At(16, 00).AsUtc();
            stats.LogEvent("myProjector", "second event");

            //-----------------------------------------------------------------------------------------------------------
            // Assert
            //-----------------------------------------------------------------------------------------------------------
            var projectorStats = stats.GetForAllProjectors().Should().ContainSingle(s => s.ProjectorId == "myProjector").Subject;
            projectorStats.GetEvents().Should().BeEquivalentTo(new[]
            {
                new
                {
                    Body = "first event",
                    TimestampUtc = nowUtc.At(15, 00)
                },
                new
                {
                    Body = "second event",
                    TimestampUtc = nowUtc.At(16, 00)
                }
            });
        }

        [Fact]
        public void When_the_projector_runs_at_a_constant_speed_it_should_use_that_to_calculate_the_eta()
        {
            //-----------------------------------------------------------------------------------------------------------
            // Arrange
            //-----------------------------------------------------------------------------------------------------------
            DateTime nowUtc = 16.June(2017).At(15, 00).AsUtc();

            var stats = new ProjectionStats(() => nowUtc);

            long transactionsPerSecond = 1000;

            //-----------------------------------------------------------------------------------------------------------
            // Act
            //-----------------------------------------------------------------------------------------------------------
            long checkpoint = 0;

            for (int seconds = 0; seconds < 60; ++seconds)
            {
                checkpoint += transactionsPerSecond;

                stats.TrackProgress("myProjector", checkpoint);

                nowUtc = nowUtc.Add(1.Seconds());
            }

            //-----------------------------------------------------------------------------------------------------------
            // Assert
            //-----------------------------------------------------------------------------------------------------------
            TimeSpan? eta = stats.GetTimeToReach("myProjector", 100000);

            stats.GetSpeed("myProjector").Should().Be(transactionsPerSecond);

            long secondsToComplete = (100000 - checkpoint) / transactionsPerSecond;

            eta.Should().Be(TimeSpan.FromSeconds(secondsToComplete));
        }

        [Fact]
        public void When_the_projector_runs_at_a_very_low_speed_it_should_still_calculate_the_eta()
        {
            //-----------------------------------------------------------------------------------------------------------
            // Arrange
            //-----------------------------------------------------------------------------------------------------------
            DateTime nowUtc = 16.June(2017).At(15, 00).AsUtc();

            var stats = new ProjectionStats(() => nowUtc);

            long transactionsPer5Seconds = 1;

            //-----------------------------------------------------------------------------------------------------------
            // Act
            //-----------------------------------------------------------------------------------------------------------
            long checkpoint = 0;

            for (int seconds = 0; seconds < 60; seconds += 5)
            {
                checkpoint += transactionsPer5Seconds;

                stats.TrackProgress("myProjector", checkpoint);

                nowUtc = nowUtc.Add(5.Seconds());
            }

            //-----------------------------------------------------------------------------------------------------------
            // Assert
            //-----------------------------------------------------------------------------------------------------------

            TimeSpan? eta = stats.GetTimeToReach("myProjector", 100000);

            long secondsToComplete =((100000 - checkpoint) / transactionsPer5Seconds * 5);

            eta.Should().BeCloseTo(TimeSpan.FromSeconds(secondsToComplete), 1000);
        }

        [Fact]
        public void When_the_projectors_speed_increases_it_should_favor_the_higher_speed_in_the_eta()
        {
            //-----------------------------------------------------------------------------------------------------------
            // Arrange
            //-----------------------------------------------------------------------------------------------------------
            DateTime nowUtc = 16.June(2017).At(15, 00).AsUtc();

            var stats = new ProjectionStats(() => nowUtc);

            long transactionsPerSecond = 1000;

            //-----------------------------------------------------------------------------------------------------------
            // Act
            //-----------------------------------------------------------------------------------------------------------
            long checkpoint = 0;

            for (int seconds = 0; seconds < 60; ++seconds)
            {
                checkpoint += transactionsPerSecond;

                stats.TrackProgress("myProjector", checkpoint);

                nowUtc = nowUtc.Add(1.Seconds());
                transactionsPerSecond += 100;
            }

            //-----------------------------------------------------------------------------------------------------------
            // Assert
            //-----------------------------------------------------------------------------------------------------------
            TimeSpan? eta = stats.GetTimeToReach("myProjector", checkpoint + 100000);

            long weightedAveragePerSecond = 4550;
            stats.GetSpeed("myProjector").Should().Be(weightedAveragePerSecond);


            long secondsToComplete = 100000 / weightedAveragePerSecond;

            eta.Should().Be(TimeSpan.FromSeconds(secondsToComplete));
        }

        [Fact]
        public void When_the_projectors_speed_decreases_it_should_favor_the_lower_speed_in_the_eta()
        {
            //-----------------------------------------------------------------------------------------------------------
            // Arrange
            //-----------------------------------------------------------------------------------------------------------
            DateTime nowUtc = 16.June(2017).At(15, 00).AsUtc();

            var stats = new ProjectionStats(() => nowUtc);

            long transactionsPerSecond = 1000;

            //-----------------------------------------------------------------------------------------------------------
            // Act
            //-----------------------------------------------------------------------------------------------------------
            long checkpoint = 0;

            for (int seconds = 0; seconds < 60; ++seconds)
            {
                checkpoint += transactionsPerSecond;

                stats.TrackProgress("myProjector", checkpoint);

                nowUtc = nowUtc.Add(1.Seconds());
                transactionsPerSecond -= 10;
            }

            //-----------------------------------------------------------------------------------------------------------
            // Assert
            //-----------------------------------------------------------------------------------------------------------
            TimeSpan? eta = stats.GetTimeToReach("myProjector", checkpoint + 100000);

            long weightedAveragePerSecond = 645;
            stats.GetSpeed("myProjector").Should().Be(weightedAveragePerSecond);

            long secondsToComplete = 100000 / weightedAveragePerSecond;

            eta.Should().Be(TimeSpan.FromSeconds(secondsToComplete));
        }

        [Fact]
        public void When_the_projector_runs_for_more_than_10_minutes_it_should_only_evaluate_the_last_10_minutes()
        {
            //-----------------------------------------------------------------------------------------------------------
            // Arrange
            //-----------------------------------------------------------------------------------------------------------
            DateTime nowUtc = 16.June(2017).At(15, 00).AsUtc();

            var stats = new ProjectionStats(() => nowUtc);

            //-----------------------------------------------------------------------------------------------------------
            // Act
            //-----------------------------------------------------------------------------------------------------------
            long checkpoint = 0;

            // The first ten minutes should be ignored
            for (int seconds = 0; seconds < (10 * 60); ++seconds)
            {
                checkpoint += 1000;

                stats.TrackProgress("myProjector", checkpoint);

                nowUtc = nowUtc.Add(1.Seconds());
            }

            // Then nine minutes of 2000/s.
            for (int seconds = 0; seconds < (9 * 60); ++seconds)
            {
                checkpoint += 2000;

                stats.TrackProgress("myProjector", checkpoint);

                nowUtc = nowUtc.Add(1.Seconds());
            }

            // The last minute should run on 3000/s
            for (int seconds = 0; seconds < (60); ++seconds)
            {
                checkpoint += 3000;

                stats.TrackProgress("myProjector", checkpoint);

                nowUtc = nowUtc.Add(1.Seconds());
            }

            //-----------------------------------------------------------------------------------------------------------
            // Assert
            //-----------------------------------------------------------------------------------------------------------
            TimeSpan? eta = stats.GetTimeToReach("myProjector", checkpoint + 100000);

            float precalculatedWeightedAveragePerSecond = 2222.5022F;

            long secondsToComplete = (long) (100000 / precalculatedWeightedAveragePerSecond);

            eta.Should().Be(TimeSpan.FromSeconds(secondsToComplete));
            stats.GetSpeed("myProjector").Should().BeApproximately(precalculatedWeightedAveragePerSecond, 1);
        }

        [Fact]
        public void When_the_projector_is_ahead_of_the_requested_checkpoint_the_eta_should_be_zero()
        {
            //-----------------------------------------------------------------------------------------------------------
            // Arrange
            //-----------------------------------------------------------------------------------------------------------
            DateTime utcNow = new DateTime(2017, 7, 4, 11, 50, 0, DateTimeKind.Utc);
            var stats = new ProjectionStats(() => utcNow);
            stats.TrackProgress("myProjector", 1000);

            //-----------------------------------------------------------------------------------------------------------
            // Act
            //-----------------------------------------------------------------------------------------------------------
            utcNow = new DateTime(2017, 7, 4, 11, 52, 0, DateTimeKind.Utc);
            stats.TrackProgress("myProjector", 10000);

            //-----------------------------------------------------------------------------------------------------------
            // Assert
            //-----------------------------------------------------------------------------------------------------------
            stats.GetSpeed("myProjector").Should().Be((10000-1000) / 120);
            stats.GetTimeToReach("myProjector", 5000).Should().Be(TimeSpan.Zero);
        }

        [Fact]
        public void When_the_projector_has_not_checked_in_yet_it_should_not_provide_an_eta()
        {
            //-----------------------------------------------------------------------------------------------------------
            // Arrange
            //-----------------------------------------------------------------------------------------------------------
            var stats = new ProjectionStats(() => DateTime.UtcNow);

            //-----------------------------------------------------------------------------------------------------------
            // Act
            //-----------------------------------------------------------------------------------------------------------
            TimeSpan? eta = stats.GetTimeToReach("myProjector", 100000);

            //-----------------------------------------------------------------------------------------------------------
            // Assert
            //-----------------------------------------------------------------------------------------------------------
            eta.Should().NotHaveValue();

            stats.GetSpeed("myProjector").Should().BeNull();
        }
    }
}
