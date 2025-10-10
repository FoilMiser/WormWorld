using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEngine;
using WormWorld.Genome;

namespace WormWorld.Runtime
{
    /// <summary>
    /// Runtime owner for a genome-backed creature lattice.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CreatureRuntime : MonoBehaviour
    {
        private readonly List<MuscleActuator2D> _actuators = new List<MuscleActuator2D>();
        private float _phase;
        private float _totalEnergy;
        private CellGridBuilder.BuildResult _grid;
        private float[] _senseBuffer = Array.Empty<float>();

        /// <summary>
        /// Active genome driving the runtime instance.
        /// </summary>
        public Genome ActiveGenome { get; private set; }

        /// <summary>
        /// Physics configuration used when initialising the lattice.
        /// </summary>
        public CellPhysicsConfig PhysicsConfig { get; private set; }

        /// <summary>
        /// Returns the number of actuators constructed for the genome.
        /// </summary>
        public int ActuatorCount => _actuators.Count;

        /// <summary>
        /// Aggregate energy spent across all actuators.
        /// </summary>
        public float TotalEnergy => _totalEnergy;

        /// <summary>
        /// Root GameObject that owns the generated cell lattice.
        /// </summary>
        public GameObject GridRoot => _grid.Root;

        /// <summary>
        /// Builds the lattice for the supplied genome and configuration.
        /// </summary>
        public void Initialize(Genome genome, CellPhysicsConfig config)
        {
            if (genome == null)
            {
                throw new ArgumentNullException(nameof(genome));
            }

            if (config == null)
            {
                throw new ArgumentNullException(nameof(config));
            }

            DisposeGrid();

            ActiveGenome = genome;
            PhysicsConfig = config;
            _grid = CellGridBuilder.Build(gameObject, genome, config);

            BuildActuators(genome.MusclesJson);
            _senseBuffer = new float[4];
            _phase = 0f;
            _totalEnergy = 0f;
        }

        /// <summary>
        /// Computes a simple observation vector for testing controllers.
        /// </summary>
        public float[] Sense()
        {
            if (_grid.Cells == null || _grid.Cells.Count == 0)
            {
                return Array.Empty<float>();
            }

            var centroid = GetCentroid();
            var velocity = GetAverageVelocity();
            var heading = velocity.sqrMagnitude > 1e-6f ? Mathf.Atan2(velocity.y, velocity.x) : 0f;
            _phase += Time.fixedDeltaTime;

            _senseBuffer[0] = centroid.x;
            _senseBuffer[1] = centroid.y;
            _senseBuffer[2] = heading;
            _senseBuffer[3] = Mathf.Sin(_phase);
            return _senseBuffer;
        }

        /// <summary>
        /// Applies controller outputs to each actuator.
        /// </summary>
        public void Actuate(IReadOnlyList<float> outputs, float dt)
        {
            if (outputs == null)
            {
                throw new ArgumentNullException(nameof(outputs));
            }

            if (_actuators.Count == 0)
            {
                return;
            }

            var count = Mathf.Min(outputs.Count, _actuators.Count);
            for (var i = 0; i < count; i++)
            {
                _actuators[i].Apply(outputs[i], dt);
                _totalEnergy += _actuators[i].LastEnergyCost;
            }
        }

        /// <summary>
        /// Computes the centroid of the cell bodies in world space.
        /// </summary>
        public Vector2 GetCentroid()
        {
            if (_grid.Cells == null || _grid.Cells.Count == 0)
            {
                return transform.position;
            }

            var sum = Vector2.zero;
            foreach (var cell in _grid.Cells.Values)
            {
                sum += cell.WorldCenter;
            }

            return sum / _grid.Cells.Count;
        }

        /// <summary>
        /// Retrieves the average rigidbody velocity across the lattice.
        /// </summary>
        public Vector2 GetAverageVelocity()
        {
            if (_grid.Cells == null || _grid.Cells.Count == 0)
            {
                return Vector2.zero;
            }

            var sum = Vector2.zero;
            foreach (var cell in _grid.Cells.Values)
            {
                if (cell.Body != null)
                {
                    sum += cell.Body.velocity;
                }
            }

            return sum / _grid.Cells.Count;
        }

        private void BuildActuators(string musclesJson)
        {
            _actuators.Clear();

            if (string.IsNullOrWhiteSpace(musclesJson))
            {
                return;
            }

            var array = JArray.Parse(musclesJson);
            foreach (var token in array.OfType<JObject>())
            {
                var gene = ParseMuscle(token);
                var actuator = new MuscleActuator2D(_grid, gene, PhysicsConfig);
                _actuators.Add(actuator);
            }
        }

        private static MuscleGene ParseMuscle(JObject token)
        {
            MuscleGene gene = default;
            gene.Id = token.Value<string>("id") ?? string.Empty;
            gene.AnchorA = new MuscleAnchorReference
            {
                Coordinate = new CellCoordinate
                {
                    Row = token["anchor_a"]?.Value<int>("row") ?? 0,
                    Col = token["anchor_a"]?.Value<int>("col") ?? 0
                }
            };
            gene.AnchorB = new MuscleAnchorReference
            {
                Coordinate = new CellCoordinate
                {
                    Row = token["anchor_b"]?.Value<int>("row") ?? 0,
                    Col = token["anchor_b"]?.Value<int>("col") ?? 0
                }
            };
            gene.NerveEndingCoord = new CellCoordinate
            {
                Row = token["nerve_ending_coord"]?.Value<int>("row") ?? 0,
                Col = token["nerve_ending_coord"]?.Value<int>("col") ?? 0
            };
            gene.RestLengthCells = token.Value<double?>("rest_length_cells") ?? 1.0;
            gene.WidthCells = token.Value<double?>("width_cells") ?? 1.0;
            gene.StrengthFactor = token.Value<double?>("strength_factor") ?? 1.0;
            gene.EnergyFactor = token.Value<double?>("energy_cost") ?? 0.0;
            return gene;
        }

        private void DisposeGrid()
        {
            if (_grid.Root != null)
            {
                if (Application.isPlaying)
                {
                    Destroy(_grid.Root);
                }
                else
                {
                    DestroyImmediate(_grid.Root);
                }
            }

            _grid = default;
            _actuators.Clear();
            _totalEnergy = 0f;
        }

        private void OnDestroy()
        {
            DisposeGrid();
        }
    }
}
