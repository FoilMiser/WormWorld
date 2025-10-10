using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityEngine;
using WormWorld.Genome;

namespace WormWorld.Runtime
{
    /// <summary>
    /// Translates genome cell grids into connected Unity physics lattices.
    /// </summary>
    public static class CellGridBuilder
    {
        /// <summary>
        /// Result of a build operation.
        /// </summary>
        public struct BuildResult
        {
            public GameObject Root;
            public Dictionary<(int x, int y), CellBody> Cells;
        }

        private static readonly (int dx, int dy)[] CardinalOffsets =
        {
            (1, 0),
            (0, 1)
        };

        private static readonly (int dx, int dy)[] DiagonalOffsets =
        {
            (1, 1),
            (1, -1)
        };

        /// <summary>
        /// Builds a deterministic set of physics bodies for the provided genome.
        /// </summary>
        public static BuildResult Build(GameObject parent, Genome genome, CellPhysicsConfig cfg)
        {
            if (parent == null)
            {
                throw new ArgumentNullException(nameof(parent));
            }

            if (genome == null)
            {
                throw new ArgumentNullException(nameof(genome));
            }

            if (cfg == null)
            {
                throw new ArgumentNullException(nameof(cfg));
            }

            var bodyJson = genome.BodyJson;
            if (string.IsNullOrWhiteSpace(bodyJson))
            {
                throw new InvalidOperationException("Genome is missing body_json payload.");
            }

            var bodyToken = JObject.Parse(bodyJson);
            if (!(bodyToken["cells"] is JArray cellsArray))
            {
                throw new InvalidOperationException("Genome body_json does not include a 'cells' array.");
            }

            var cellEntries = ExtractCells(cellsArray);
            if (cellEntries.Count == 0)
            {
                throw new InvalidOperationException("Genome contains no cells to build.");
            }

            var root = new GameObject(string.IsNullOrEmpty(genome.Name) ? $"Creature_{genome.Id}" : genome.Name)
            {
                hideFlags = HideFlags.None
            };
            root.transform.SetParent(parent.transform, false);

            var materialCache = new Dictionary<TissueType, PhysicsMaterial2D>();
            var cellMap = new Dictionary<(int x, int y), CellBody>();

            foreach (var entry in cellEntries)
            {
                var key = (entry.col, entry.row);
                if (cellMap.ContainsKey(key))
                {
                    continue;
                }

                var cellObject = new GameObject($"Cell_{entry.col}_{entry.row}_{entry.tissue}")
                {
                    hideFlags = HideFlags.None
                };
                cellObject.transform.SetParent(root.transform, false);
                cellObject.transform.localPosition = new Vector3(entry.col * cfg.cellSize, -entry.row * cfg.cellSize, 0f);

                var rigidbody = cellObject.AddComponent<Rigidbody2D>();
                rigidbody.bodyType = RigidbodyType2D.Dynamic;
                rigidbody.gravityScale = 1f;
                rigidbody.drag = cfg.baseDamping;
                rigidbody.angularDrag = cfg.baseDamping;
                rigidbody.interpolation = RigidbodyInterpolation2D.None;

                var overrides = cfg.GetOverride(entry.tissue);
                rigidbody.mass = Mathf.Max(0.01f, cfg.baseMass * (float)entry.area * Mathf.Max(0.01f, overrides.MassMultiplier));
                rigidbody.drag = cfg.baseDamping * Mathf.Max(0f, overrides.DragMultiplier);
                rigidbody.angularDrag = cfg.baseDamping * Mathf.Max(0f, overrides.DragMultiplier);

                var collider = cellObject.AddComponent<BoxCollider2D>();
                collider.size = new Vector2(cfg.cellSize, cfg.cellSize);
                collider.usedByComposite = false;
                collider.autoTiling = false;
                collider.sharedMaterial = GetMaterialForTissue(cfg, overrides, entry.tissue, materialCache);

                var cellBody = cellObject.AddComponent<CellBody>();
                cellBody.Initialise(entry.col, entry.row, entry.tissue, rigidbody, collider);

                cellMap.Add(key, cellBody);
            }

            CreateNeighbourJoints(cellEntries, cellMap, cfg);

            return new BuildResult
            {
                Root = root,
                Cells = cellMap
            };
        }

        private static void CreateNeighbourJoints(IReadOnlyList<CellEntry> orderedCells, Dictionary<(int x, int y), CellBody> cellMap, CellPhysicsConfig cfg)
        {
            var offsets = new List<(int dx, int dy)>(CardinalOffsets);
            if (cfg.diagonalJoints)
            {
                offsets.AddRange(DiagonalOffsets);
            }

            foreach (var entry in orderedCells)
            {
                foreach (var offset in offsets)
                {
                    var neighbourKey = (entry.col + offset.dx, entry.row + offset.dy);
                    if (!cellMap.TryGetValue(neighbourKey, out var neighbour))
                    {
                        continue;
                    }

                    var self = cellMap[(entry.col, entry.row)];
                    if (neighbour.Body == null || self.Body == null)
                    {
                        continue;
                    }

                    var joint = self.gameObject.AddComponent<DistanceJoint2D>();
                    joint.autoConfigureDistance = false;
                    joint.connectedBody = neighbour.Body;
                    joint.anchor = Vector2.zero;
                    joint.connectedAnchor = Vector2.zero;
                    joint.enableCollision = false;
                    joint.frequency = cfg.neighborJointFrequency;
                    joint.dampingRatio = cfg.neighborJointDampingRatio;

                    var distance = Mathf.Sqrt(offset.dx * offset.dx + offset.dy * offset.dy);
                    if (distance <= Mathf.Epsilon)
                    {
                        distance = 1f;
                    }

                    joint.distance = distance * cfg.cellSize;
                }
            }
        }

        private static PhysicsMaterial2D GetMaterialForTissue(CellPhysicsConfig cfg, TissueMaterialOverride overrides, TissueType tissue, Dictionary<TissueType, PhysicsMaterial2D> cache)
        {
            if (cache.TryGetValue(tissue, out var material))
            {
                return material;
            }

            material = new PhysicsMaterial2D($"{tissue}_Cell")
            {
                friction = cfg.baseFriction * Mathf.Max(0f, overrides.FrictionMultiplier),
                bounciness = Mathf.Clamp01(overrides.BouncinessMultiplier)
            };

            cache.Add(tissue, material);
            return material;
        }

        private static List<CellEntry> ExtractCells(JArray cellsArray)
        {
            var entries = new List<CellEntry>(cellsArray.Count);
            foreach (var token in cellsArray)
            {
                if (token is not JObject cellObject)
                {
                    continue;
                }

                var coord = cellObject["coord"] as JObject ?? throw new InvalidOperationException("Cell is missing coord object.");
                var tissueValue = cellObject.Value<string>("tissue") ?? throw new InvalidOperationException("Cell missing tissue field.");
                if (!Enum.TryParse(tissueValue, ignoreCase: false, out TissueType tissue))
                {
                    throw new InvalidOperationException($"Unknown tissue type '{tissueValue}'.");
                }

                var row = coord.Value<int>("row");
                var col = coord.Value<int>("col");
                var area = cellObject.Value<double?>("area") ?? 1.0;

                entries.Add(new CellEntry(row, col, tissue, area));
            }

            entries.Sort((a, b) =>
            {
                var rowCompare = a.row.CompareTo(b.row);
                return rowCompare != 0 ? rowCompare : a.col.CompareTo(b.col);
            });

            return entries;
        }

        private readonly struct CellEntry
        {
            public readonly int row;
            public readonly int col;
            public readonly TissueType tissue;
            public readonly double area;

            public CellEntry(int row, int col, TissueType tissue, double area)
            {
                this.row = row;
                this.col = col;
                this.tissue = tissue;
                this.area = area;
            }
        }
    }
}
