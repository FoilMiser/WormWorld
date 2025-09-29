using System;

namespace WormWorld.Genome
{
    /// <summary>
    /// Enumerates the supported cell tissue types.
    /// </summary>
    public enum TissueType
    {
        Throat,
        Digestive,
        Brain,
        Eye,
        Reproductive,
        MuscleAnchor,
        Fat,
        Skin,
        Armor,
        PheromoneEmitter,
        PheromoneReceptor,
        NerveEnding
    }

    /// <summary>
    /// Enumerates available edge geometry styles for cells.
    /// </summary>
    public enum EdgeShape
    {
        Square,
        Rounded,
        Spiked,
        Tapered
    }

    /// <summary>
    /// Serializable coordinate pair using CSV row/column order.
    /// </summary>
    [Serializable]
    public struct CellCoordinate
    {
        /// <summary>
        /// Zero-based row index within the body grid.
        /// </summary>
        public int Row;

        /// <summary>
        /// Zero-based column index within the body grid.
        /// </summary>
        public int Col;
    }

    /// <summary>
    /// Material and structural properties describing a cell's tissue characteristics.
    /// </summary>
    [Serializable]
    public struct MaterialGene
    {
        /// <summary>
        /// Elasticity coefficient (0-1).
        /// </summary>
        public double Elasticity;

        /// <summary>
        /// Flexibility coefficient (0-1).
        /// </summary>
        public double Flexibility;

        /// <summary>
        /// Toughness coefficient (0-1).
        /// </summary>
        public double Toughness;

        /// <summary>
        /// Resistance to impulsive/shock forces (0-1).
        /// </summary>
        public double ShockResistance;

        /// <summary>
        /// Resistance to sustained forces (0-1).
        /// </summary>
        public double ContinuousResistance;
    }

    /// <summary>
    /// Expanded representation for cell metadata matching the JSON schema.
    /// </summary>
    [Serializable]
    public struct CellGene
    {
        /// <summary>
        /// CSV-style coordinate of the cell.
        /// </summary>
        public CellCoordinate Coord;

        /// <summary>
        /// Tissue category.
        /// </summary>
        public TissueType Tissue;

        /// <summary>
        /// Relative area used as a density proxy (0.01-10).
        /// </summary>
        public double Area;

        /// <summary>
        /// Material property bundle.
        /// </summary>
        public MaterialGene Material;

        /// <summary>
        /// Edge contact shape modifier.
        /// </summary>
        public EdgeShape EdgeShape;
    }
}