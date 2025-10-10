using System;

namespace WormWorld.Genome
{
    /// <summary>
    /// Reference to a muscle anchor, either by cell index or explicit coordinate.
    /// </summary>
    [Serializable]
    public struct MuscleAnchorReference
    {
        /// <summary>
        /// Optional zero-based index into the flattened cell array.
        /// </summary>
        public int? CellIndex;

        /// <summary>
        /// Optional explicit coordinate of the anchor cell.
        /// </summary>
        public CellCoordinate? Coordinate;
    }

    /// <summary>
    /// Serializable description of a contractile muscle line between two anchors.
    /// </summary>
    [Serializable]
    public struct MuscleGene
    {
        /// <summary>
        /// Unique identifier for the muscle instance.
        /// </summary>
        public string Id;

        /// <summary>
        /// First anchor reference.
        /// </summary>
        public MuscleAnchorReference AnchorA;

        /// <summary>
        /// Second anchor reference.
        /// </summary>
        public MuscleAnchorReference AnchorB;

        /// <summary>
        /// Coordinate of the nerve ending controlling this muscle.
        /// </summary>
        public CellCoordinate NerveEndingCoord;

        /// <summary>
        /// Resting length of the muscle in grid cells (>= 1).
        /// </summary>
        public double RestLengthCells;

        /// <summary>
        /// Effective width of the muscle in grid cells (>= 1).
        /// </summary>
        public double WidthCells;

        /// <summary>
        /// Strength multiplier applied to base force output (>= 0.1).
        /// </summary>
        public double StrengthFactor;

        /// <summary>
        /// Energy multiplier describing per-tick muscle upkeep (>= 0).
        /// </summary>
        public double EnergyFactor;
    }
}
