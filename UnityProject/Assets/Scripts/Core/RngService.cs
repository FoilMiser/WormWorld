using System;

namespace WormWorld.Core
{
    /// <summary>
    /// Deterministic façade over <see cref="System.Random"/> to keep Unity-side randomness centralized.
    /// </summary>
    public sealed class RngService
    {
        private readonly Random _random;

        /// <summary>
        /// Initializes the RNG with a deterministic seed.
        /// </summary>
        /// <param name="seed">Seed value that controls the generated sequence.</param>
        public RngService(int seed)
        {
            _random = new Random(seed);
        }

        /// <summary>
        /// Gets the next floating-point value in [0,1).
        /// </summary>
        public float NextFloat01()
        {
            return (float)_random.NextDouble();
        }

        /// <summary>
        /// Gets the next integer within the specified bounds.
        /// </summary>
        /// <param name="minInclusive">Inclusive lower bound.</param>
        /// <param name="maxExclusive">Exclusive upper bound.</param>
        public int NextInt(int minInclusive, int maxExclusive)
        {
            return _random.Next(minInclusive, maxExclusive);
        }

        /// <summary>
        /// Samples a Gaussian (normal) distribution using the Box-Muller transform.
        /// </summary>
        /// <param name="mean">Desired mean of the distribution.</param>
        /// <param name="standardDeviation">Standard deviation of the distribution.</param>
        public float NextGaussian(float mean, float standardDeviation)
        {
            // Box-Muller transform using two uniform values in (0, 1].
            var u1 = 1.0 - _random.NextDouble();
            var u2 = 1.0 - _random.NextDouble();
            var radius = Math.Sqrt(-2.0 * Math.Log(u1));
            var angle = 2.0 * Math.PI * u2;
            var z = radius * Math.Cos(angle);
            return (float)(mean + standardDeviation * z);
        }
    }
}
