using System;
using System.Collections;
using System.IO;
using Newtonsoft.Json.Linq;
using UnityEngine;
using WormWorld.Genome;

namespace WormWorld.Runtime
{
    /// <summary>
    /// Loads a genome and spins up a runtime creature with a scripted controller.
    /// </summary>
    [RequireComponent(typeof(CreatureRuntime))]
    public sealed class CreatureSpawner : MonoBehaviour
    {
        [Header("Genome Source")]
        [Tooltip("JSONL asset containing at least one expanded genome row.")]
        public TextAsset genomeJsonl;

        [Tooltip("Optional filesystem path to a JSONL file. Used when no TextAsset is supplied.")]
        public string genomeJsonlPath;

        [Header("Runtime Configuration")]
        public CellPhysicsConfig cellConfig;
        public GameObject groundPrefab;

        [Tooltip("Number of fixed steps to simulate per episode.")]
        [Min(1)]
        public int stepsPerEpisode = 600;

        [Header("Controller")]
        [Range(0f, 1f)]
        public float controllerAmplitude = 0.6f;

        [Min(0f)]
        public float controllerFrequency = 1.2f;

        private CreatureRuntime _runtime;
        private Vector2 _startCentroid;

        private IEnumerator Start()
        {
            if (cellConfig == null)
            {
                Debug.LogError("CreatureSpawner requires a CellPhysicsConfig asset.", this);
                yield break;
            }

            _runtime = GetComponent<CreatureRuntime>();
            if (_runtime == null)
            {
                Debug.LogError("CreatureSpawner requires a CreatureRuntime component.", this);
                yield break;
            }

            if (groundPrefab != null)
            {
                Instantiate(groundPrefab, Vector3.zero, Quaternion.identity);
            }

            var genome = LoadGenome();
            if (genome == null)
            {
                yield break;
            }

            _runtime.Initialize(genome, cellConfig);
            _startCentroid = _runtime.GetCentroid();

            var controller = gameObject.AddComponent<ScriptedOscillatorController>();
            controller.amplitude = controllerAmplitude;
            controller.frequency = controllerFrequency;

            yield return StartCoroutine(RunEpisode(genome));
        }

        private IEnumerator RunEpisode(Genome genome)
        {
            var steps = Mathf.Max(1, stepsPerEpisode);
            var maxDistance = 0f;

            for (var i = 0; i < steps; i++)
            {
                yield return new WaitForFixedUpdate();
                var centroid = _runtime.GetCentroid();
                var distance = Vector2.Distance(_startCentroid, centroid);
                if (distance > maxDistance)
                {
                    maxDistance = distance;
                }
            }

            Debug.Log($"[CreatureSpawner] Genome {genome.Id} distance={maxDistance:F3} energy={_runtime.TotalEnergy:F3}", this);
        }

        private Genome LoadGenome()
        {
            try
            {
                var json = ExtractGenomeLine();
                if (string.IsNullOrEmpty(json))
                {
                    Debug.LogError("Genome JSONL is empty or missing.", this);
                    return null;
                }

                var token = JToken.Parse(json);
                var genome = new Genome
                {
                    Version = token.Value<string>("version") ?? "v0",
                    Id = token.Value<string>("id") ?? "demo",
                    Name = token.Value<string>("name") ?? "Demo Creature",
                    Seed = token.Value<ulong?>("seed") ?? 0UL,
                    MetadataJson = token["metadata"]?.ToString(Newtonsoft.Json.Formatting.None) ?? "{}",
                    BodyJson = token["body"]?.ToString(Newtonsoft.Json.Formatting.None) ?? "{}",
                    BrainJson = token["brain"]?.ToString(Newtonsoft.Json.Formatting.None) ?? "{}",
                    SensesJson = token["senses"]?.ToString(Newtonsoft.Json.Formatting.None) ?? "{}",
                    ReproductionJson = token["reproduction"]?.ToString(Newtonsoft.Json.Formatting.None) ?? "{}",
                    MusclesJson = token["muscles"]?.ToString(Newtonsoft.Json.Formatting.None) ?? "[]",
                    PheromonesJson = token["pheromone_pairs"]?.ToString(Newtonsoft.Json.Formatting.None) ?? "[]",
                    NervesJson = token["nerves"]?.ToString(Newtonsoft.Json.Formatting.None) ?? "{}",
                    EnergyJson = token["energy"]?.ToString(Newtonsoft.Json.Formatting.None) ?? "{}",
                    FitnessJson = token["fitness_weights"]?.ToString(Newtonsoft.Json.Formatting.None) ?? "{}",
                    PreEvalFitness = token.Value<double?>("pre_eval_fitness")
                };

                return genome;
            }
            catch (Exception ex)
            {
                Debug.LogError($"Failed to load genome JSONL: {ex.Message}", this);
                return null;
            }
        }

        private string ExtractGenomeLine()
        {
            if (genomeJsonl != null && !string.IsNullOrEmpty(genomeJsonl.text))
            {
                using var reader = new StringReader(genomeJsonl.text);
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    if (!string.IsNullOrWhiteSpace(line))
                    {
                        return line.Trim();
                    }
                }
            }

            if (!string.IsNullOrEmpty(genomeJsonlPath))
            {
                var path = genomeJsonlPath;
#if UNITY_EDITOR
                if (!Path.IsPathRooted(path))
                {
                    path = Path.GetFullPath(Path.Combine(Application.dataPath, "..", path));
                }
#endif
                if (File.Exists(path))
                {
                    foreach (var line in File.ReadLines(path))
                    {
                        if (!string.IsNullOrWhiteSpace(line))
                        {
                            return line.Trim();
                        }
                    }
                }
            }

            return string.Empty;
        }
    }
}
