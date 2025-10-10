using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Linq;
using WormWorld.Genome;

namespace WormWorld.GA
{
    /// <summary>
    /// Applies deterministic, schema-aware mutations to genomes without invoking the runtime simulation.
    /// </summary>
    public static class Mutation
    {
        private const double StrengthFactorMinimum = 0.1;
        private const double VisionFieldMin = 1.0;
        private const double VisionFieldMax = 360.0;
        private const int VisionRangeMin = 1;
        private const int VisionRangeMax = 128;

        private static readonly JsonSerializerSettings SerializerSettings = new JsonSerializerSettings
        {
            NullValueHandling = NullValueHandling.Include,
            DateParseHandling = DateParseHandling.None,
            FloatParseHandling = FloatParseHandling.Double,
            Formatting = Formatting.None
        };

        /// <summary>
        /// Mutates the supplied genome using deterministic random streams and configured bounds.
        /// </summary>
        /// <param name="source">Source genome to mutate.</param>
        /// <param name="config">Mutation configuration.</param>
        /// <param name="rng">Deterministic RNG instance.</param>
        /// <returns>A new genome instance with mutations applied.</returns>
        public static Genome.Genome Apply(Genome.Genome source, GAConfig config, DeterministicRng rng)
        {
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            if (config == null)
            {
                throw new ArgumentNullException(nameof(config));
            }

            var child = GenomeCloneUtility.Clone(source);

            MutateMuscles(child, config, rng);
            MutateHiddenNodes(child, config, rng);
            MutateVision(child, config, rng);
            MutatePheromones(child, config, rng);
            MutateMaterials(child, config, rng);

            return child;
        }

        private static void MutateMuscles(Genome.Genome genome, GAConfig config, DeterministicRng rng)
        {
            if (string.IsNullOrEmpty(genome.MusclesJson) || config.MuscleMutationRate <= 0f)
            {
                return;
            }

            var array = ParseArray(genome.MusclesJson);
            if (array == null || array.Count == 0)
            {
                return;
            }

            var changed = false;
            foreach (var muscle in array.OfType<JObject>())
            {
                var currentStrength = muscle.Value<double?>("strength_factor");
                if (currentStrength == null)
                {
                    continue;
                }

                if (!rng.NextBool(config.MuscleMutationRate))
                {
                    continue;
                }

                var jitter = rng.NextSymmetric(config.StrengthFactorJitter);
                var next = Clamp(currentStrength.Value + jitter, StrengthFactorMinimum, config.MaxStrengthFactor);
                if (Math.Abs(next - currentStrength.Value) > double.Epsilon)
                {
                    muscle["strength_factor"] = next;
                    changed = true;
                }
            }

            if (changed)
            {
                genome.MusclesJson = CanonicalizeToken(array);
            }
        }

        private static void MutateHiddenNodes(Genome.Genome genome, GAConfig config, DeterministicRng rng)
        {
            if (string.IsNullOrEmpty(genome.BrainJson) || config.HiddenNodeMutationRate <= 0f)
            {
                return;
            }

            var brain = JsonConvert.DeserializeObject<BrainPayload>(genome.BrainJson, SerializerSettings);
            if (brain == null)
            {
                return;
            }

            if (!rng.NextBool(config.HiddenNodeMutationRate))
            {
                return;
            }

            var constraints = NeuralConstraints.Calculate(brain.CellCount);
            brain.HiddenNodes.Max = Math.Min(brain.HiddenNodes.Max, constraints.MaxHiddenNodes);
            brain.LayerLimit = Math.Max(1, Math.Min(brain.LayerLimit, constraints.MaxLayers));

            if (rng.NextBool())
            {
                if (brain.HiddenNodes.Used < brain.HiddenNodes.Max)
                {
                    brain.HiddenNodes.Used++;
                }
                else if (brain.Layers.Used < brain.LayerLimit)
                {
                    brain.Layers.Used++;
                    brain.HiddenNodes.Max = Math.Min(brain.HiddenNodes.Max + constraints.HiddenBudgetPerNewLayer, constraints.MaxHiddenNodes);
                    brain.HiddenNodes.Used = Math.Min(brain.HiddenNodes.Used + 1, brain.HiddenNodes.Max);
                }
            }
            else if (brain.HiddenNodes.Used > 0)
            {
                brain.HiddenNodes.Used--;
                if (brain.Layers.Used > 1 && brain.HiddenNodes.Used <= brain.HiddenNodes.Max - constraints.HiddenBudgetPerNewLayer)
                {
                    brain.Layers.Used--;
                    brain.HiddenNodes.Max = Math.Max(brain.HiddenNodes.Used, brain.HiddenNodes.Max - constraints.HiddenBudgetPerNewLayer);
                }
            }

            brain.HiddenNodes.Used = Math.Max(0, Math.Min(brain.HiddenNodes.Used, brain.HiddenNodes.Max));
            brain.Layers.Used = Math.Max(1, Math.Min(brain.Layers.Used, brain.LayerLimit));

            genome.BrainJson = Canonicalize(brain);
        }

        private static void MutateVision(Genome.Genome genome, GAConfig config, DeterministicRng rng)
        {
            if (string.IsNullOrEmpty(genome.SensesJson) || config.VisionMutationRate <= 0f)
            {
                return;
            }

            var senses = JsonConvert.DeserializeObject<SensesPayload>(genome.SensesJson, SerializerSettings);
            if (senses == null || senses.Vision == null || senses.Vision.EyeCellCount <= 0)
            {
                return;
            }

            if (!rng.NextBool(config.VisionMutationRate))
            {
                return;
            }

            var vision = senses.Vision;
            var fieldDelta = Math.Max(1.0, 360.0 * config.VisionTradeoffStep);
            var rangeDelta = Math.Max(1, (int)Math.Round(128 * config.VisionTradeoffStep));

            if (rng.NextBool())
            {
                vision.FieldOfViewDeg = Clamp(vision.FieldOfViewDeg + fieldDelta, VisionFieldMin, VisionFieldMax);
                vision.RangeCells = Clamp(vision.RangeCells - rangeDelta, VisionRangeMin, VisionRangeMax);
            }
            else
            {
                vision.FieldOfViewDeg = Clamp(vision.FieldOfViewDeg - fieldDelta, VisionFieldMin, VisionFieldMax);
                vision.RangeCells = Clamp(vision.RangeCells + rangeDelta, VisionRangeMin, VisionRangeMax);
            }

            vision.ProcessingEnergy = Clamp01(vision.ProcessingEnergy + rng.NextSymmetric(config.VisionEnergyStep));
            vision.ClarityFalloff = Math.Max(0.0, vision.ClarityFalloff + rng.NextSymmetric(config.VisionClarityStep));

            genome.SensesJson = Canonicalize(senses);
        }

        private static void MutatePheromones(Genome.Genome genome, GAConfig config, DeterministicRng rng)
        {
            if (string.IsNullOrEmpty(genome.PheromonesJson) || config.PheromoneToggleProbability <= 0f)
            {
                return;
            }

            var array = ParseArray(genome.PheromonesJson);
            if (array == null || array.Count == 0)
            {
                return;
            }

            if (!rng.NextBool(config.PheromoneToggleProbability))
            {
                return;
            }

            var index = rng.NextInt(0, array.Count);
            if (array[index] is not JObject pair)
            {
                return;
            }

            var emitter = pair["emitter"] as JObject;
            var receptor = pair["receptor"] as JObject;
            if (emitter == null || receptor == null)
            {
                return;
            }

            var currentEnergy = emitter.Value<double?>("specialization_energy") ?? 0.0;
            var currentlyEnabled = currentEnergy > config.PheromoneDisableThreshold;
            var targetEnergy = currentlyEnabled ? 0.0 : Clamp01(config.PheromoneEnableValue);

            emitter["specialization_energy"] = targetEnergy;
            receptor["specialization_energy"] = targetEnergy;

            genome.PheromonesJson = CanonicalizeToken(array);
        }

        private static void MutateMaterials(Genome.Genome genome, GAConfig config, DeterministicRng rng)
        {
            if (string.IsNullOrEmpty(genome.BodyJson) || config.MaterialMutationRate <= 0f)
            {
                return;
            }

            var body = ParseObject(genome.BodyJson);
            var cells = body?["cells"] as JArray;
            if (cells == null || cells.Count == 0)
            {
                return;
            }

            var changed = false;
            foreach (var cell in cells.OfType<JObject>())
            {
                if (!rng.NextBool(config.MaterialMutationRate))
                {
                    continue;
                }

                var material = cell["material"] as JObject;
                if (material == null)
                {
                    continue;
                }

                var elasticity = material.Value<double?>("elasticity") ?? 0.0;
                var toughness = material.Value<double?>("toughness") ?? 0.0;

                var nextElasticity = Clamp01(elasticity + rng.NextSymmetric(config.MaterialNudge));
                var nextToughness = Clamp01(toughness + rng.NextSymmetric(config.MaterialNudge));

                if (Math.Abs(nextElasticity - elasticity) < double.Epsilon &&
                    Math.Abs(nextToughness - toughness) < double.Epsilon)
                {
                    continue;
                }

                material["elasticity"] = nextElasticity;
                material["toughness"] = nextToughness;
                changed = true;
            }

            if (changed)
            {
                genome.BodyJson = CanonicalizeToken(body);
            }
        }

        private static string CanonicalizeToken(JToken token) => GenomeIO.CanonicalizeSection(token.ToString(Formatting.None));

        private static string Canonicalize(object instance)
        {
            if (instance == null)
            {
                throw new ArgumentNullException(nameof(instance));
            }

            var serializer = JsonSerializer.Create(SerializerSettings);
            var token = JToken.FromObject(instance, serializer);
            return CanonicalizeToken(token);
        }

        private static JArray? ParseArray(string json) => string.IsNullOrWhiteSpace(json) ? null : JArray.Parse(json);

        private static JObject? ParseObject(string json) => string.IsNullOrWhiteSpace(json) ? null : JObject.Parse(json);

        private static double Clamp(double value, double min, double max)
        {
            if (value < min)
            {
                return min;
            }

            if (value > max)
            {
                return max;
            }

            return value;
        }

        private static int Clamp(int value, int min, int max)
        {
            if (value < min)
            {
                return min;
            }

            if (value > max)
            {
                return max;
            }

            return value;
        }

        private static double Clamp01(double value)
        {
            if (value < 0)
            {
                return 0;
            }

            if (value > 1)
            {
                return 1;
            }

            return value;
        }

        private class BrainPayload
        {
            [JsonProperty("cell_count")]
            public int CellCount { get; set; }

            [JsonProperty("hidden_nodes")]
            public HiddenNodePayload HiddenNodes { get; set; } = new HiddenNodePayload();

            [JsonProperty("layer_limit")]
            public int LayerLimit { get; set; }

            [JsonProperty("layers")]
            public LayerPayload Layers { get; set; } = new LayerPayload();
        }

        private class HiddenNodePayload
        {
            [JsonProperty("max")]
            public int Max { get; set; }

            [JsonProperty("used")]
            public int Used { get; set; }
        }

        private class LayerPayload
        {
            [JsonProperty("used")]
            public int Used { get; set; }
        }

        private class SensesPayload
        {
            [JsonProperty("vision")]
            public VisionPayload? Vision { get; set; }
        }

        private class VisionPayload
        {
            [JsonProperty("eye_cell_count")]
            public int EyeCellCount { get; set; }

            [JsonProperty("field_of_view_deg")]
            public double FieldOfViewDeg { get; set; }

            [JsonProperty("range_cells")]
            public int RangeCells { get; set; }

            [JsonProperty("processing_energy")]
            public double ProcessingEnergy { get; set; }

            [JsonProperty("clarity_falloff")]
            public double ClarityFalloff { get; set; }
        }

    }
}
