using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using WormWorld.Genome;

namespace WormWorld.Runtime
{
    /// <summary>
    /// Applies deterministic forces for a genome muscle between two cell anchors.
    /// </summary>
    public sealed class MuscleActuator2D
    {
        private const float DeltaGain = 0.6f;
        private const float RateGain = 12f;
        private const float MaxForce = 250f;

        private readonly CellGridBuilder.BuildResult _grid;
        private readonly MuscleGene _gene;
        private readonly CellBody _anchorA;
        private readonly CellBody _anchorB;

        /// <summary>
        /// Initialises a new actuator for the supplied muscle.
        /// </summary>
        public MuscleActuator2D(CellGridBuilder.BuildResult grid, MuscleGene gene, CellPhysicsConfig config)
        {
            _grid = grid;
            _gene = gene;
            Config = config ?? throw new ArgumentNullException(nameof(config));
            _anchorA = ResolveAnchor(gene.AnchorA);
            _anchorB = ResolveAnchor(gene.AnchorB);

            if (_anchorA == null || _anchorB == null)
            {
                throw new InvalidOperationException($"Failed to resolve anchors for muscle '{gene.Id}'.");
            }
        }

        /// <summary>
        /// Configuration used when computing forces.
        /// </summary>
        public CellPhysicsConfig Config { get; }

        /// <summary>
        /// Total accumulated energy expenditure.
        /// </summary>
        public float TotalEnergyCost { get; private set; }

        /// <summary>
        /// Energy spent during the most recent application.
        /// </summary>
        public float LastEnergyCost { get; private set; }

        /// <summary>
        /// Applies the deterministic muscle force for a simulation step.
        /// </summary>
        /// <param name="activation">Activation in [-1, 1] controlling contraction or extension.</param>
        /// <param name="dt">Timestep in seconds.</param>
        public void Apply(float activation, float dt)
        {
            if (_anchorA?.Body == null || _anchorB?.Body == null)
            {
                LastEnergyCost = 0f;
                return;
            }

            activation = Mathf.Clamp(activation, -1f, 1f);
            var rbA = _anchorA.Body;
            var rbB = _anchorB.Body;

            var positionA = rbA.worldCenterOfMass;
            var positionB = rbB.worldCenterOfMass;
            var delta = positionB - positionA;
            var distance = delta.magnitude;
            if (distance < 1e-5f)
            {
                LastEnergyCost = 0f;
                return;
            }

            var direction = delta / distance;
            var desiredDelta = activation * (float)_gene.StrengthFactor * Config.cellSize * DeltaGain;
            var desiredRate = desiredDelta / Mathf.Max(dt, 1e-4f);
            var relativeSpeed = Vector2.Dot(rbB.velocity - rbA.velocity, direction);
            var forceMagnitude = Mathf.Clamp((desiredRate - relativeSpeed) * RateGain, -MaxForce, MaxForce);
            var force = direction * forceMagnitude;

            rbA.AddForceAtPosition(force, positionA, ForceMode2D.Force);
            rbB.AddForceAtPosition(-force, positionB, ForceMode2D.Force);

            var power = Mathf.Abs(forceMagnitude * relativeSpeed);
            LastEnergyCost = power * dt * (float)_gene.EnergyFactor;
            TotalEnergyCost += LastEnergyCost;
        }

        private CellBody ResolveAnchor(MuscleAnchorReference anchor)
        {
            if (anchor.Coordinate.HasValue)
            {
                var coord = anchor.Coordinate.Value;
                if (_grid.Cells.TryGetValue((coord.Col, coord.Row), out var cell))
                {
                    return cell;
                }

                return FindNearestCell(coord.Col, coord.Row);
            }

            if (anchor.CellIndex.HasValue)
            {
                var ordered = _grid.Cells
                    .OrderBy(pair => pair.Key.y)
                    .ThenBy(pair => pair.Key.x)
                    .Select(pair => pair.Value)
                    .ToList();

                if (ordered.Count == 0)
                {
                    return null;
                }

                var index = Mathf.Clamp(anchor.CellIndex.Value, 0, ordered.Count - 1);
                return ordered[index];
            }

            return null;
        }

        private CellBody FindNearestCell(int x, int y)
        {
            var bestDistance = float.MaxValue;
            CellBody best = null;
            foreach (var pair in _grid.Cells)
            {
                var dx = Mathf.Abs(pair.Key.x - x);
                var dy = Mathf.Abs(pair.Key.y - y);
                var distance = dx + dy;
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = pair.Value;
                    if (distance <= 0f)
                    {
                        break;
                    }
                }
            }

            return best;
        }
    }
}
