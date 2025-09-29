using System;

namespace WormWorld.Genome
{
    /// <summary>
    /// Default scoring weights applied during fitness evaluation.
    /// </summary>
    [Serializable]
    public class FitnessWeights
    {
        /// <summary>
        /// Weight applied to locomotion distance objectives (0-10).
        /// </summary>
        public double Distance { get; set; } = 0.5;

        /// <summary>
        /// Weight applied to remaining energy objectives (0-10).
        /// </summary>
        public double Energy { get; set; } = 0.2;

        /// <summary>
        /// Weight applied to scenario task completion (0-10).
        /// </summary>
        public double Task { get; set; } = 0.3;
    }
}