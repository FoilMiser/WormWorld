using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
using WormWorld.Genome;
using WormWorld.Runtime;

namespace WormWorld.Tests.EditMode
{
    public class CellGridBuilderTests
    {
        [Test]
        public void BuildProducesDeterministicCellsAndJoints()
        {
            var genome = CreateTestGenome();
            var config = CreateConfig();

            var parentA = new GameObject("ParentA");
            var parentB = new GameObject("ParentB");

            var resultA = CellGridBuilder.Build(parentA, genome, config);
            var resultB = CellGridBuilder.Build(parentB, genome, config);

            Assert.AreEqual(resultA.Cells.Count, resultB.Cells.Count, "Cell count mismatch.");
            Assert.That(Mathf.Abs(SumMass(resultA) - SumMass(resultB)), Is.LessThan(1e-4f), "Mass totals diverged.");

            var jointsA = CollectJointPairs(resultA);
            var jointsB = CollectJointPairs(resultB);
            Assert.That(jointsA.SetEquals(jointsB), "Neighbour joint pairs differ between builds.");

            Cleanup(resultA);
            Cleanup(resultB);
            Object.DestroyImmediate(parentA);
            Object.DestroyImmediate(parentB);
        }

        [Test]
        public void AllCardinalNeighboursAreConnected()
        {
            var genome = CreateTestGenome();
            var config = CreateConfig();
            var parent = new GameObject("NeighbourCheck");
            var result = CellGridBuilder.Build(parent, genome, config);

            var offsets = new[] { (1, 0), (0, 1) };
            foreach (var kvp in result.Cells)
            {
                foreach (var offset in offsets)
                {
                    var neighbourKey = (kvp.Key.x + offset.Item1, kvp.Key.y + offset.Item2);
                    if (!result.Cells.TryGetValue(neighbourKey, out var neighbour))
                    {
                        continue;
                    }

                    Assert.IsTrue(HasJointBetween(kvp.Value, neighbour), $"Missing joint between {kvp.Key} and {neighbourKey}.");
                }
            }

            Cleanup(result);
            Object.DestroyImmediate(parent);
        }

        private static Genome CreateTestGenome()
        {
            var cells = new JArray
            {
                CreateCell(0, 0, "Brain"),
                CreateCell(0, 1, "Brain"),
                CreateCell(1, 0, "MuscleAnchor"),
                CreateCell(1, 1, "MuscleAnchor")
            };

            var body = new JObject
            {
                ["grid"] = new JObject
                {
                    ["width"] = 2,
                    ["height"] = 2
                },
                ["cells"] = cells
            };

            return new Genome
            {
                Version = "v0",
                Id = "test",
                Name = "test",
                BodyJson = body.ToString(Newtonsoft.Json.Formatting.None),
                MetadataJson = "{}",
                BrainJson = "{}",
                SensesJson = "{}",
                ReproductionJson = "{}",
                MusclesJson = "[]",
                PheromonesJson = "[]",
                NervesJson = "{}",
                EnergyJson = "{}",
                FitnessJson = "{}"
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
            config.neighborJointFrequency = 5f;
            config.neighborJointDampingRatio = 0.4f;
            config.diagonalJoints = false;
            return config;
        }

        private static float SumMass(CellGridBuilder.BuildResult result)
        {
            var total = 0f;
            foreach (var cell in result.Cells.Values)
            {
                if (cell.Body != null)
                {
                    total += cell.Body.mass;
                }
            }

            return total;
        }

        private static HashSet<string> CollectJointPairs(CellGridBuilder.BuildResult result)
        {
            var pairs = new HashSet<string>();
            foreach (var cell in result.Cells.Values)
            {
                foreach (var joint in cell.GetComponents<DistanceJoint2D>())
                {
                    if (joint.connectedBody == null)
                    {
                        continue;
                    }

                    if (!joint.connectedBody.TryGetComponent<CellBody>(out var other))
                    {
                        continue;
                    }

                    var key = NormalisePair(cell, other);
                    pairs.Add(key);
                }
            }

            return pairs;
        }

        private static string NormalisePair(CellBody a, CellBody b)
        {
            var aKey = new Vector2Int(a.GridX, a.GridY);
            var bKey = new Vector2Int(b.GridX, b.GridY);
            return aKey.y < bKey.y || (aKey.y == bKey.y && aKey.x <= bKey.x)
                ? $"{aKey.x},{aKey.y}:{bKey.x},{bKey.y}"
                : $"{bKey.x},{bKey.y}:{aKey.x},{aKey.y}";
        }

        private static bool HasJointBetween(CellBody a, CellBody b)
        {
            return HasJoint(a, b) || HasJoint(b, a);
        }

        private static bool HasJoint(CellBody origin, CellBody target)
        {
            foreach (var joint in origin.GetComponents<DistanceJoint2D>())
            {
                if (joint.connectedBody == target.Body)
                {
                    return true;
                }
            }

            return false;
        }

        private static void Cleanup(CellGridBuilder.BuildResult result)
        {
            if (result.Root == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Object.Destroy(result.Root);
            }
            else
            {
                Object.DestroyImmediate(result.Root);
            }
        }
    }
}
