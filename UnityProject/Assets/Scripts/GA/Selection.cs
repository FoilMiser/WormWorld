using System;
using System.Collections.Generic;

namespace WormWorld.GA
{
    /// <summary>
    /// Tournament selection operating solely on placeholder fitness values.
    /// </summary>
    public static class Selection
    {
        /// <summary>
        /// Runs a deterministic tournament and returns the winning population member.
        /// </summary>
        /// <param name="members">Available population members.</param>
        /// <param name="tournamentSize">Number of competitors in the tournament.</param>
        /// <param name="rng">Deterministic RNG source.</param>
        /// <returns>Winning population member.</returns>
        public static PopulationMember Tournament(IReadOnlyList<PopulationMember> members, int tournamentSize, DeterministicRng rng)
        {
            if (members == null)
            {
                throw new ArgumentNullException(nameof(members));
            }

            if (members.Count == 0)
            {
                throw new ArgumentException("Population must contain at least one member.", nameof(members));
            }

            if (tournamentSize <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(tournamentSize));
            }

            var best = members[rng.NextInt(0, members.Count)];

            for (var i = 1; i < tournamentSize; i++)
            {
                var contender = members[rng.NextInt(0, members.Count)];
                if (IsBetter(contender, best))
                {
                    best = contender;
                }
            }

            return best;
        }

        private static bool IsBetter(PopulationMember candidate, PopulationMember incumbent)
        {
            if (candidate.Fitness > incumbent.Fitness)
            {
                return true;
            }

            if (candidate.Fitness < incumbent.Fitness)
            {
                return false;
            }

            return string.CompareOrdinal(candidate.Genome.Id, incumbent.Genome.Id) < 0;
        }
    }
}
