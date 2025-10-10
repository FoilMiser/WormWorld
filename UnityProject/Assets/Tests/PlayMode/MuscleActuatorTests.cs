using System.Collections;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using WormWorld.Genome;
using WormWorld.Runtime;

namespace WormWorld.Tests.PlayMode
{
    public class MuscleActuatorTests
    {
        [UnityTest]
        public IEnumerator ContractingAndExtendingAdjustsDistance()
        {
            var genome = CreateMuscleGenome();
            var config = CreateConfig();
            var go = new GameObject("MuscleCreature");
            var runtime = go.AddComponent<CreatureRuntime>();
            runtime.Initialize(genome, config);

            var initialDistance = GetMuscleDistance(runtime);
            for (var i = 0; i < 20; i++)
            {
                runtime.Actuate(new[] { 1f }, Time.fixedDeltaTime);
                yield return new WaitForFixedUpdate();
            }

            var contractedDistance = GetMuscleDistance(runtime);
            Assert.Less(contractedDistance, initialDistance, "Expected muscle contraction to reduce distance.");

            for (var i = 0; i < 20; i++)
            {
                runtime.Actuate(new[] { -1f }, Time.fixedDeltaTime);
                yield return new WaitForFixedUpdate();
            }

            var extendedDistance = GetMuscleDistance(runtime);
            Assert.Greater(extendedDistance, contractedDistance, "Expected extension to increase distance.");

            Object.Destroy(go);
            ScriptableObject.Destroy(config);
            yield return null;
        }

        [UnityTest]
        public IEnumerator IdenticalEpisodesRemainDeterministic()
        {
            var config = CreateConfig();
            var trajectoryA = new List<Vector2>();
            var trajectoryB = new List<Vector2>();

            yield return RunEpisode(CreateMuscleGenome(), config, trajectoryA);
            yield return RunEpisode(CreateMuscleGenome(), config, trajectoryB);

            Assert.AreEqual(trajectoryA.Count, trajectoryB.Count, "Trajectory lengths differ.");
            for (var i = 0; i < trajectoryA.Count; i++)
            {
                var delta = trajectoryA[i] - trajectoryB[i];
                Assert.Less(delta.sqrMagnitude, 1e-10f, $"Determinism break at step {i}.");
            }

            ScriptableObject.Destroy(config);
        }

        private static IEnumerator RunEpisode(Genome genome, CellPhysicsConfig config, IList<Vector2> trajectory)
        {
            var go = new GameObject("DeterministicCreature");
            var runtime = go.AddComponent<CreatureRuntime>();
            runtime.Initialize(genome, config);

            var controller = go.AddComponent<ScriptedOscillatorController>();
            controller.amplitude = 0.5f;
            controller.frequency = 1.5f;

            const int steps = 60;
            for (var i = 0; i < steps; i++)
            {
                yield return new WaitForFixedUpdate();
                trajectory.Add(runtime.GetCentroid());
            }

            Object.Destroy(go);
            yield return null;
        }

        private static float GetMuscleDistance(CreatureRuntime runtime)
        {
            var bodies = runtime.GridRoot.GetComponentsInChildren<CellBody>();
            if (bodies.Length < 2)
            {
                return 0f;
            }

            return Vector2.Distance(bodies[0].WorldCenter, bodies[1].WorldCenter);
        }

        private static Genome CreateMuscleGenome()
        {
            var cells = new JArray
            {
                CreateCell(0, 0, "MuscleAnchor"),
                CreateCell(0, 1, "MuscleAnchor")
            };

            var body = new JObject
            {
                ["grid"] = new JObject { ["width"] = 2, ["height"] = 1 },
                ["cells"] = cells
            };

            var muscles = new JArray
            {
                new JObject
                {
                    ["id"] = "m0",
                    ["anchor_a"] = new JObject { ["row"] = 0, ["col"] = 0 },
                    ["anchor_b"] = new JObject { ["row"] = 0, ["col"] = 1 },
                    ["nerve_ending_coord"] = new JObject { ["row"] = 0, ["col"] = 0 },
                    ["rest_length_cells"] = 1.0,
                    ["width_cells"] = 1.0,
                    ["strength_factor"] = 1.0,
                    ["energy_cost"] = 0.1
                }
            };

            return new Genome
            {
                Version = "v0",
                Id = "muscle",
                Name = "muscle",
                BodyJson = body.ToString(Newtonsoft.Json.Formatting.None),
                MetadataJson = "{}",
                BrainJson = "{}",
                SensesJson = "{}",
                ReproductionJson = "{}",
                MusclesJson = muscles.ToString(Newtonsoft.Json.Formatting.None),
                PheromonesJson = "[]",
                NervesJson = "{}",
                EnergyJson = "{}",
                FitnessJson = "{}",
                Seed = 1234
            };
        }

        private static JObject CreateCell(int row, int col, string tissue)
        {
            return new JObject
            {
                ["coord"] = new JObject { ["row"] = row, ["col"] = col },
                ["tissue"] = tissue,
                ["area"] = 1.0,
                ["material"] = new JObject
                {
                    ["elasticity"] = 0.5,
                    ["flexibility"] = 0.5,
                    ["toughness"] = 0.5,
                    ["shock_resistance"] = 0.5,
                    ["continuous_resistance"] = 0.5
                },
                ["edge_shape"] = "Square"
            };
        }

        private static CellPhysicsConfig CreateConfig()
        {
            var config = ScriptableObject.CreateInstance<CellPhysicsConfig>();
            config.cellSize = 0.5f;
            config.baseMass = 1f;
            config.baseDamping = 0.2f;
            config.baseFriction = 0.5f;
            config.neighborJointFrequency = 6f;
            config.neighborJointDampingRatio = 0.5f;
            config.diagonalJoints = false;
            return config;
        }
    }
}
