using System;

namespace WormWorld.Genome
{
    /// <summary>
    /// Helper methods for computing derived neural architecture limits.
    /// </summary>
    public static class NeuralConstraints
    {
        /// <summary>
        /// Computes constraint values from a brain cell count following the v0 rules.
        /// </summary>
        /// <param name="brainCellCount">Number of brain cells available (&gt;= 1).</param>
        /// <returns>Derived limits on hidden nodes and layers.</returns>
        public static NeuralConstraintResult Calculate(int brainCellCount)
        {
            if (brainCellCount <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(brainCellCount), "Brain cell count must be positive.");
            }

            var maxHiddenNodes = brainCellCount * brainCellCount;
            var maxLayers = Math.Max(1, brainCellCount / 2);
            return new NeuralConstraintResult(maxHiddenNodes, maxLayers, brainCellCount);
        }
    }

    /// <summary>
    /// Container for neural constraint outputs.
    /// </summary>
    [Serializable]
    public struct NeuralConstraintResult
    {
        /// <summary>
        /// Initializes the result structure.
        /// </summary>
        public NeuralConstraintResult(int maxHiddenNodes, int maxLayers, int hiddenBudgetPerNewLayer)
        {
            MaxHiddenNodes = maxHiddenNodes;
            MaxLayers = maxLayers;
            HiddenBudgetPerNewLayer = hiddenBudgetPerNewLayer;
        }

        /// <summary>
        /// Maximum allowed hidden nodes (brain_cell_count^2).
        /// </summary>
        public int MaxHiddenNodes { get; }

        /// <summary>
        /// Maximum allowed hidden layers (floor(brain_cell_count / 2), minimum 1).
        /// </summary>
        public int MaxLayers { get; }

        /// <summary>
        /// Additional hidden node budget granted per new layer (equals brain_cell_count).
        /// </summary>
        public int HiddenBudgetPerNewLayer { get; }
    }
}