using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace LiquidProjections.Statistics
{
    /// <summary>
    /// Calculates the weighted speed in transactions per second.
    /// </summary>
    /// <remarks>
    /// This class is not thread-safe.
    /// A <see cref="Monitor"/> or another synchronization method should be used to ensure thread-safe usage. 
    /// </remarks>
    public class WeightedProjectionSpeedCalculator
    {
        private readonly TimeSpan threshold;
        private readonly int maxNrOfSamples;
        private readonly Queue<float> samples = new Queue<float>();
        private DateTime? lastSampleTimeStampUtc;
        private long? lastCheckpoint;

        public WeightedProjectionSpeedCalculator(int maxNrOfSamples, TimeSpan threshold)
        {
            this.maxNrOfSamples = maxNrOfSamples;
            this.threshold = threshold;
        }

        private bool HasBaselineBeenSet => lastSampleTimeStampUtc != null;
        
        public void Record(long checkpoint, DateTime timestampUtc)
        {
            if (HasBaselineBeenSet)
            {
                TimeSpan interval = timestampUtc - lastSampleTimeStampUtc.Value;

                if (interval > threshold)
                {
                    long delta = checkpoint - lastCheckpoint.Value;

                    samples.Enqueue((float) (delta / interval.TotalSeconds));

                    lastCheckpoint = checkpoint;
                    lastSampleTimeStampUtc = timestampUtc;

                    DiscardOlderSamples();
                }
            }
            else
            {
                SetBaseline(checkpoint, timestampUtc);
            }
        }

        private void SetBaseline(long checkpoint, DateTime timestampUtc)
        {
            lastCheckpoint = checkpoint;
            lastSampleTimeStampUtc = timestampUtc;
        }

        private void DiscardOlderSamples()
        {
            while (samples.Count > maxNrOfSamples)
            {
                samples.Dequeue();
            }
        }

        public float? GetWeightedSpeedIncluding(float sample)
        {
            return GetWeightedSpeed(samples.Concat(new[] { sample }));
        }

        public float? GetWeightedSpeed()
        {
            return GetWeightedSpeed(samples);
        }
        
        public float? GetWeightedSpeed(IEnumerable<float> effectiveSamples)
        {
            float weightedSum = 0;
            int weights = 0;
            int weight = 0;
            
            foreach (float sample in effectiveSamples)
            {
                weight++;
                weights += weight;
                weightedSum += sample * weight;
            }

            return (weights == 0) ? (float?) null : weightedSum / weights;
        }
    }
}