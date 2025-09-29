using System;

namespace WormWorld.Genome
{
    /// <summary>
    /// Serializable representation of the v0 genome header and scalar fields.
    /// Complex collections are stored as JSON strings for flexible expansion.
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
        /// Width of the body grid in cells (>= 1).
        /// </summary>
        public int GridWidth { get; set; }

        /// <summary>
        /// Height of the body grid in cells (>= 1).
        /// </summary>
        public int GridHeight { get; set; }

        /// <summary>
        /// JSON packed list of cell genes in CSV coordinate order.
        /// </summary>
        public string CellsJson { get; set; }

        /// <summary>
        /// JSON packed material presets for cells (values within 0-1).
        /// </summary>
        public string MaterialsJson { get; set; }

        /// <summary>
        /// Count of brain tissue cells (>= 1).
        /// </summary>
        public int BrainCellCount { get; set; }

        /// <summary>
        /// Neural network input node count (>= 1, equals brain cell count).
        /// </summary>
        public int BrainInputNodeCount { get; set; }

        /// <summary>
        /// Maximum hidden nodes derived from brain cell count (>= 0, typically brain^2).
        /// </summary>
        public int BrainHiddenNodeMax { get; set; }

        /// <summary>
        /// Currently allocated hidden nodes (>= 0).
        /// </summary>
        public int BrainHiddenNodeUsed { get; set; }

        /// <summary>
        /// Maximum allowed hidden layers (>= 1, floor(brain_cell_count / 2)).
        /// </summary>
        public int BrainLayerLimit { get; set; }

        /// <summary>
        /// Hidden layers currently instantiated (>= 1).
        /// </summary>
        public int BrainLayersUsed { get; set; }

        /// <summary>
        /// Count of eye cells available for vision processing (>= 0).
        /// </summary>
        public int EyeCellCount { get; set; }

        /// <summary>
        /// Horizontal field of view in degrees (1-360).
        /// </summary>
        public double FieldOfViewDegrees { get; set; }

        /// <summary>
        /// Vision range in grid cells (1-128).
        /// </summary>
        public int VisionRangeCells { get; set; }

        /// <summary>
        /// Fraction of energy budget dedicated to visual processing (0-1).
        /// </summary>
        public double VisionProcessingEnergy { get; set; }

        /// <summary>
        /// Information clarity falloff per grid cell (>= 0).
        /// </summary>
        public double VisionClarityFalloff { get; set; }

        /// <summary>
        /// Number of pheromone emitter/receptor pairs (>= 0).
        /// </summary>
        public int PheromonePairCount { get; set; }

        /// <summary>
        /// Fraction of energy budget dedicated to pheromone processing (0-1).
        /// </summary>
        public double PheromoneProcessingEnergy { get; set; }

        /// <summary>
        /// Pheromone sensory focus mode (TrailTracking, FieldSampling, BroadcastAnalysis).
        /// </summary>
        public string PheromoneInformationFocus { get; set; }

        /// <summary>
        /// JSON packed pheromone pair definitions (emitters and receptors).
        /// </summary>
        public string PheromonesJson { get; set; }

        /// <summary>
        /// Count of reproductive cells (>= 0).
        /// </summary>
        public int ReproductiveCellCount { get; set; }

        /// <summary>
        /// Reproductive strategy mode.
        /// </summary>
        public ReproductionMode ReproductionMode { get; set; }

        /// <summary>
        /// Gestation duration in simulation ticks (>= 1).
        /// </summary>
        public int GestationTicks { get; set; }

        /// <summary>
        /// Fraction of parent energy allocated per offspring (0-1).
        /// </summary>
        public double OffspringEnergyRatio { get; set; }

        /// <summary>
        /// JSON packed set of muscle definitions.
        /// </summary>
        public string MusclesJson { get; set; }

        /// <summary>
        /// JSON packed nerve ending records.
        /// </summary>
        public string NerveEndingsJson { get; set; }

        /// <summary>
        /// JSON packed nerve cluster definitions.
        /// </summary>
        public string NerveClustersJson { get; set; }

        /// <summary>
        /// Share of energy reserved for vision subsystems (0-1).
        /// </summary>
        public double EnergyVisionShare { get; set; }

        /// <summary>
        /// Share of energy reserved for movement and muscles (0-1).
        /// </summary>
        public double EnergyMovementShare { get; set; }

        /// <summary>
        /// Share of energy reserved for pheromone signaling (0-1).
        /// </summary>
        public double EnergyPheromoneShare { get; set; }

        /// <summary>
        /// Share of energy reserved for brain maintenance (0-1).
        /// </summary>
        public double EnergyBrainShare { get; set; }

        /// <summary>
        /// JSON packed per-objective fitness weight table.
        /// </summary>
        public string FitnessWeightsJson { get; set; }
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