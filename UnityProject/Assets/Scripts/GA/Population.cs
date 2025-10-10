using System;
using System.Collections.Generic;
using WormWorld.Genome;

namespace WormWorld.GA
{
    /// <summary>
    /// Represents a deterministic population of genomes along with their cached placeholder fitness values.
    /// </summary>
    [Serializable]
    public class Population
    {
        private readonly List<PopulationMember> _members;

        /// <summary>
        /// Initializes a new population instance.
        /// </summary>
        /// <param name="members">Ordered sequence of population members.</param>
        /// <param name="seed">Deterministic seed controlling GA randomness.</param>
        public Population(IEnumerable<PopulationMember> members, ulong seed)
        {
            if (members == null)
            {
                throw new ArgumentNullException(nameof(members));
            }

            _members = new List<PopulationMember>(members);
            Seed = seed;
        }

        /// <summary>
        /// Seed that drives deterministic random streams for the GA.
        /// </summary>
        public ulong Seed { get; }

        /// <summary>
        /// Read-only view of the population members in their deterministic order.
        /// </summary>
        public IReadOnlyList<PopulationMember> Members => _members;

        /// <summary>
        /// Gets the number of members in the population.
        /// </summary>
        public int Count => _members.Count;

        /// <summary>
        /// Creates a deterministic RNG instance derived from the population seed and optional stream keys.
        /// </summary>
        /// <param name="streamKeys">Additional integers that partition the deterministic random streams.</param>
        /// <returns>Deterministic RNG seeded from the population seed and stream keys.</returns>
        public DeterministicRng CreateRng(params int[] streamKeys)
        {
            return DeterministicRng.Create(Seed, streamKeys);
        }
    }

    /// <summary>
    /// Immutable pairing of a genome and its cached placeholder fitness.
    /// </summary>
    [Serializable]
    public class PopulationMember
    {
        /// <summary>
        /// Initializes the population member.
        /// </summary>
        /// <param name="genome">Genome payload.</param>
        /// <param name="fitness">Placeholder fitness value.</param>
        public PopulationMember(Genome.Genome genome, double fitness)
        {
            Genome = genome ?? throw new ArgumentNullException(nameof(genome));
            Fitness = fitness;
        }

        /// <summary>
        /// Genome payload for the member.
        /// </summary>
        public Genome.Genome Genome { get; }

        /// <summary>
        /// Placeholder fitness value used for selection prior to simulation evaluation.
        /// </summary>
        public double Fitness { get; }
    }

    /// <summary>
    /// Deterministic SplitMix64-based RNG for GA operations.
    /// </summary>
    public struct DeterministicRng
    {
        private ulong _state;

        private DeterministicRng(ulong state)
        {
            _state = state;
        }

        /// <summary>
        /// Creates a deterministic RNG by combining the base seed with additional stream keys.
        /// </summary>
        /// <param name="baseSeed">Base 64-bit seed.</param>
        /// <param name="streamKeys">Ordered stream identifiers to derive sub-streams.</param>
        /// <returns>Initialized deterministic RNG.</returns>
        public static DeterministicRng Create(ulong baseSeed, params int[] streamKeys)
        {
            ulong state = baseSeed ^ 0x9E3779B97F4A7C15UL;
            if (streamKeys != null)
            {
                foreach (var key in streamKeys)
                {
                    state = SplitMix64(state + unchecked((ulong)(key + 0x632BE59B)));
                }
            }

            if (state == 0)
            {
                state = 0x9E3779B97F4A7C15UL;
            }

            return new DeterministicRng(state);
        }

        /// <summary>
        /// Produces a double in the range [0, 1).
        /// </summary>
        public double NextDouble()
        {
            return (NextUInt64() >> 11) * (1.0 / (1UL << 53));
        }

        /// <summary>
        /// Produces an unsigned 64-bit integer.
        /// </summary>
        public ulong NextUInt64()
        {
            _state = SplitMix64(_state);
            return _state;
        }

        /// <summary>
        /// Produces an integer within the specified range.
        /// </summary>
        /// <param name="minInclusive">Minimum inclusive value.</param>
        /// <param name="maxExclusive">Maximum exclusive value.</param>
        public int NextInt(int minInclusive, int maxExclusive)
        {
            if (maxExclusive <= minInclusive)
            {
                throw new ArgumentOutOfRangeException(nameof(maxExclusive), "maxExclusive must be greater than minInclusive.");
            }

            var range = (ulong)(maxExclusive - minInclusive);
            return (int)(minInclusive + (long)(NextUInt64() % range));
        }

        /// <summary>
        /// Returns true with the supplied probability.
        /// </summary>
        public bool NextBool(double probability = 0.5)
        {
            if (probability <= 0)
            {
                return false;
            }

            if (probability >= 1)
            {
                return true;
            }

            return NextDouble() < probability;
        }

        /// <summary>
        /// Returns a symmetric random offset scaled by the supplied magnitude.
        /// </summary>
        /// <param name="magnitude">Maximum absolute value of the offset.</param>
        public double NextSymmetric(double magnitude)
        {
            return (NextDouble() * 2.0 - 1.0) * magnitude;
        }

        private static ulong SplitMix64(ulong x)
        {
            x += 0x9E3779B97F4A7C15UL;
            x = (x ^ (x >> 30)) * 0xBF58476D1CE4E5B9UL;
            x = (x ^ (x >> 27)) * 0x94D049BB133111EBUL;
            return x ^ (x >> 31);
        }
    }

    /// <summary>
    /// Utility helpers for cloning genomes without mutating source instances.
    /// </summary>
    internal static class GenomeCloneUtility
    {
        public static Genome.Genome Clone(Genome.Genome source)
        {
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            return new Genome.Genome
            {
                Version = source.Version,
                Id = source.Id,
                Name = source.Name,
                Seed = source.Seed,
                MetadataJson = source.MetadataJson,
                BodyJson = source.BodyJson,
                BrainJson = source.BrainJson,
                SensesJson = source.SensesJson,
                ReproductionJson = source.ReproductionJson,
                MusclesJson = source.MusclesJson,
                PheromonesJson = source.PheromonesJson,
                NervesJson = source.NervesJson,
                EnergyJson = source.EnergyJson,
                FitnessJson = source.FitnessJson,
                PreEvalFitness = source.PreEvalFitness
            };
        }
    }
}
