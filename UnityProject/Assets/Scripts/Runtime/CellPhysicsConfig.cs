using System;
using System.Collections.Generic;
using UnityEngine;
using WormWorld.Genome;

namespace WormWorld.Runtime
{
    /// <summary>
    /// Tuning parameters that control how genome cells map to physics bodies.
    /// </summary>
    [CreateAssetMenu(fileName = "CellPhysicsConfig", menuName = "WormWorld/Cell Physics Config", order = 10)]
    public sealed class CellPhysicsConfig : ScriptableObject
    {
        [Tooltip("World units spanned by each grid cell.")]
        [Min(0.01f)]
        public float cellSize = 0.5f;

        [Tooltip("Base mass applied to a unit-area cell before tissue multipliers.")]
        [Min(0f)]
        public float baseMass = 1f;

        [Tooltip("Base linear and angular damping applied to each cell.")]
        [Min(0f)]
        public float baseDamping = 1f;

        [Tooltip("Base friction applied to the cell's collider.")]
        [Min(0f)]
        public float baseFriction = 0.8f;

        [Tooltip("Target joint frequency used to bind neighbouring cells.")]
        [Min(0f)]
        public float neighborJointFrequency = 5f;

        [Tooltip("Damping ratio for the neighbour joints.")]
        [Range(0f, 1f)]
        public float neighborJointDampingRatio = 0.4f;

        [Tooltip("When enabled, connect diagonal neighbours with joints as well as cardinal ones.")]
        public bool diagonalJoints = true;

        [Tooltip("Optional per-tissue overrides for physics material behaviour.")]
        public List<TissueMaterialOverride> materialOverrides = new List<TissueMaterialOverride>();

        private readonly Dictionary<TissueType, TissueMaterialOverride> _overrideLookup = new Dictionary<TissueType, TissueMaterialOverride>();

        /// <summary>
        /// Looks up the override for the supplied tissue, returning neutral defaults when absent.
        /// </summary>
        public TissueMaterialOverride GetOverride(TissueType tissue)
        {
            RebuildLookup();
            return _overrideLookup.TryGetValue(tissue, out var value) ? value : TissueMaterialOverride.Default;
        }

        private void OnValidate()
        {
            RebuildLookup();
        }

        private void RebuildLookup()
        {
            _overrideLookup.Clear();
            foreach (var entry in materialOverrides)
            {
                _overrideLookup[entry.Tissue] = entry;
            }
        }
    }

    /// <summary>
    /// Serializable multiplier set for tissue-specific physics behaviour.
    /// </summary>
    [Serializable]
    public struct TissueMaterialOverride
    {
        public TissueType Tissue;

        [Min(0f)]
        public float MassMultiplier;

        [Min(0f)]
        public float FrictionMultiplier;

        [Min(0f)]
        public float BouncinessMultiplier;

        [Min(0f)]
        public float DragMultiplier;

        /// <summary>
        /// Neutral override when no specific entry is configured.
        /// </summary>
        public static TissueMaterialOverride Default => new TissueMaterialOverride
        {
            Tissue = TissueType.Skin,
            MassMultiplier = 1f,
            FrictionMultiplier = 1f,
            BouncinessMultiplier = 0f,
            DragMultiplier = 1f
        };
    }
}
