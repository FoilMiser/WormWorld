using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using WormWorld.Genome;

namespace WormWorld.GA
{
    /// <summary>
    /// Performs deterministic uniform crossover across genome fields.
    /// </summary>
    public static class Crossover
    {
        /// <summary>
        /// Combines two parent genomes into a deterministic child genome.
        /// </summary>
        /// <param name="left">First parent.</param>
        /// <param name="right">Second parent.</param>
        /// <param name="rng">Deterministic RNG stream.</param>
        /// <returns>New genome assembled from parent fields.</returns>
        public static Genome.Genome Combine(Genome.Genome left, Genome.Genome right, DeterministicRng rng)
        {
            if (left == null)
            {
                throw new ArgumentNullException(nameof(left));
            }

            if (right == null)
            {
                throw new ArgumentNullException(nameof(right));
            }

            var child = new Genome.Genome
            {
                Version = rng.NextBool() ? left.Version : right.Version,
                Name = rng.NextBool() ? left.Name : right.Name,
                Seed = rng.NextUInt64(),
                MetadataJson = rng.NextBool() ? left.MetadataJson : right.MetadataJson,
                BodyJson = MergeBody(left.BodyJson, right.BodyJson, rng),
                BrainJson = rng.NextBool() ? left.BrainJson : right.BrainJson,
                SensesJson = rng.NextBool() ? left.SensesJson : right.SensesJson,
                ReproductionJson = rng.NextBool() ? left.ReproductionJson : right.ReproductionJson,
                MusclesJson = MergeArray(left.MusclesJson, right.MusclesJson, rng),
                PheromonesJson = rng.NextBool() ? left.PheromonesJson : right.PheromonesJson,
                NervesJson = rng.NextBool() ? left.NervesJson : right.NervesJson,
                EnergyJson = rng.NextBool() ? left.EnergyJson : right.EnergyJson,
                FitnessJson = rng.NextBool() ? left.FitnessJson : right.FitnessJson,
                PreEvalFitness = Average(left.PreEvalFitness, right.PreEvalFitness)
            };

            child.Id = BuildChildId(left.Id, right.Id, child.Seed);

            return child;
        }

        private static string BuildChildId(string leftId, string rightId, ulong seed)
        {
            var ordered = new[] { leftId ?? string.Empty, rightId ?? string.Empty };
            Array.Sort(ordered, StringComparer.Ordinal);
            return $"{ordered[0]}_{ordered[1]}_{seed:X8}";
        }

        private static double? Average(double? a, double? b)
        {
            if (!a.HasValue && !b.HasValue)
            {
                return null;
            }

            var first = a ?? 0.0;
            var second = b ?? 0.0;
            return (first + second) / 2.0;
        }

        private static string MergeBody(string leftJson, string rightJson, DeterministicRng rng)
        {
            if (string.IsNullOrEmpty(leftJson))
            {
                return rightJson;
            }

            if (string.IsNullOrEmpty(rightJson))
            {
                return leftJson;
            }

            var leftRoot = JObject.Parse(leftJson);
            var rightRoot = JObject.Parse(rightJson);

            var gridSource = rng.NextBool() ? leftRoot["grid"] : rightRoot["grid"];
            var gridElement = gridSource?.DeepClone() ?? new JObject();
            var leftCells = ExtractArray(leftRoot, "cells");
            var rightCells = ExtractArray(rightRoot, "cells");
            var mergedCells = MergeTokens(leftCells, rightCells, rng);

            var result = new JObject
            {
                ["grid"] = gridElement,
                ["cells"] = new JArray(mergedCells.Select(token => token.DeepClone()))
            };

            return GenomeIO.CanonicalizeSection(result.ToString(Formatting.None));
        }

        private static string MergeArray(string leftJson, string rightJson, DeterministicRng rng)
        {
            if (string.IsNullOrEmpty(leftJson))
            {
                return rightJson;
            }

            if (string.IsNullOrEmpty(rightJson))
            {
                return leftJson;
            }

            var leftRoot = JToken.Parse(leftJson);
            var rightRoot = JToken.Parse(rightJson);

            var merged = MergeTokens(ExtractArray(leftRoot), ExtractArray(rightRoot), rng);
            var array = new JArray(merged.Select(token => token.DeepClone()));

            return GenomeIO.CanonicalizeSection(array.ToString(Formatting.None));
        }

        private static List<JToken> ExtractArray(JObject root, string propertyName)
        {
            if (!root.TryGetValue(propertyName, out var element) || element?.Type != JTokenType.Array)
            {
                return new List<JToken>();
            }

            return ExtractArray(element);
        }

        private static List<JToken> ExtractArray(JToken arrayToken)
        {
            if (arrayToken is not JArray array)
            {
                return new List<JToken>();
            }

            return array.Select(item => item.DeepClone()).ToList();
        }

        private static List<JToken> MergeTokens(IReadOnlyList<JToken> left, IReadOnlyList<JToken> right, DeterministicRng rng)
        {
            var max = Math.Max(left.Count, right.Count);
            var merged = new List<JToken>(max);

            for (var i = 0; i < max; i++)
            {
                var hasLeft = i < left.Count;
                var hasRight = i < right.Count;

                if (hasLeft && hasRight)
                {
                    merged.Add((rng.NextBool() ? left[i] : right[i]).DeepClone());
                }
                else if (hasLeft)
                {
                    merged.Add(left[i].DeepClone());
                }
                else if (hasRight)
                {
                    merged.Add(right[i].DeepClone());
                }
            }

            return merged;
        }
    }
}
