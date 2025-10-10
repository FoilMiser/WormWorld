using UnityEngine;

namespace WormWorld.GA
{
    /// <summary>
    /// ScriptableObject container for offline GA tuning parameters.
    /// </summary>
    [CreateAssetMenu(fileName = "GAConfig", menuName = "WormWorld/GA Config")]
    public class GAConfig : ScriptableObject
    {
        [Header("General")]
        [Tooltip("Deterministic seed for GA operations.")]
        public ulong Seed = 1;

        [Min(1)]
        [Tooltip("Number of generations to evolve before writing the output CSV.")]
        public int Generations = 10;

        [Min(1)]
        [Tooltip("Number of participants in each tournament selection.")]
        public int TournamentSize = 3;

        [Header("Mutation Rates")]
        [Range(0f, 1f)]
        [Tooltip("Probability that each muscle strength factor is jittered.")]
        public float MuscleMutationRate = 0.35f;

        [Range(0f, 1f)]
        [Tooltip("Probability that hidden node counts are adjusted.")]
        public float HiddenNodeMutationRate = 0.25f;

        [Range(0f, 1f)]
        [Tooltip("Probability that vision trade-offs are adjusted.")]
        public float VisionMutationRate = 0.5f;

        [Range(0f, 1f)]
        [Tooltip("Probability of toggling a pheromone pair on/off.")]
        public float PheromoneToggleProbability = 0.15f;

        [Range(0f, 1f)]
        [Tooltip("Probability that a material sample receives an elasticity/toughness nudge.")]
        public float MaterialMutationRate = 0.2f;

        [Header("Mutation Magnitudes")]
        [Range(0f, 2f)]
        [Tooltip("Maximum absolute jitter applied to strength_factor when mutating muscles.")]
        public float StrengthFactorJitter = 0.25f;

        [Min(0.1f)]
        [Tooltip("Upper bound applied when clamping mutated strength_factor values.")]
        public float MaxStrengthFactor = 5f;

        [Range(0f, 0.5f)]
        [Tooltip("Normalized trade-off delta between field of view and range.")]
        public float VisionTradeoffStep = 0.05f;

        [Range(0f, 0.5f)]
        [Tooltip("Energy budget jitter for vision processing.")]
        public float VisionEnergyStep = 0.05f;

        [Range(0f, 0.2f)]
        [Tooltip("Adjustment magnitude for clarity falloff.")]
        public float VisionClarityStep = 0.02f;

        [Range(0f, 1f)]
        [Tooltip("Specialization energy applied when a pheromone pair is toggled on.")]
        public float PheromoneEnableValue = 0.5f;

        [Range(0f, 1f)]
        [Tooltip("Threshold below which a pheromone pair is considered disabled.")]
        public float PheromoneDisableThreshold = 0.05f;

        [Range(0f, 0.5f)]
        [Tooltip("Maximum absolute nudge applied to elasticity/toughness.")]
        public float MaterialNudge = 0.05f;
    }
}
