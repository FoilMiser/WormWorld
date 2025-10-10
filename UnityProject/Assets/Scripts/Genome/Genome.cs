using System;

namespace WormWorld.Genome
{
    /// <summary>
    /// Serializable representation of the v0 genome header and nested payloads.
    /// Complex sections are stored as canonical JSON strings.
    /// </summary>
    [Serializable]
    public class Genome
    {
        /// <summary>
        /// Schema version identifier. Always "v0" for this data contract.
        /// </summary>
        public string Version { get; set; } = "v0";

        /// <summary>
        /// Unique genome identifier (1-64 characters).
        /// </summary>
        public string Id { get; set; }

        /// <summary>
        /// Human readable genome name (1-64 characters).
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Deterministic RNG seed stored as an unsigned 64-bit integer (0-18446744073709551615).
        /// </summary>
        public ulong Seed { get; set; }

        /// <summary>
        /// JSON encoded metadata object (must include rng_service).
        /// </summary>
        public string MetadataJson { get; set; }

        /// <summary>
        /// JSON encoded body grid + cells payload.
        /// </summary>
        public string BodyJson { get; set; }

        /// <summary>
        /// JSON encoded neural architecture summary.
        /// </summary>
        public string BrainJson { get; set; }

        /// <summary>
        /// JSON encoded sensory parameters.
        /// </summary>
        public string SensesJson { get; set; }

        /// <summary>
        /// JSON encoded reproduction parameters.
        /// </summary>
        public string ReproductionJson { get; set; }

        /// <summary>
        /// JSON encoded muscle array.
        /// </summary>
        public string MusclesJson { get; set; }

        /// <summary>
        /// JSON encoded pheromone emitter/receptor pairs.
        /// </summary>
        public string PheromonesJson { get; set; }

        /// <summary>
        /// JSON encoded nerve endings and clusters payload.
        /// </summary>
        public string NervesJson { get; set; }

        /// <summary>
        /// JSON encoded energy budget summary.
        /// </summary>
        public string EnergyJson { get; set; }

        /// <summary>
        /// JSON encoded per-objective fitness weights.
        /// </summary>
        public string FitnessJson { get; set; }

        /// <summary>
        /// Optional placeholder fitness value populated before deterministic simulation runs (0-1 range recommended).
        /// </summary>
        public double? PreEvalFitness { get; set; }
    }

    /// <summary>
    /// Enumerates the supported reproductive strategy trade-offs.
    /// </summary>
    public enum ReproductionMode
    {
        Fast,
        Slow
    }
}
