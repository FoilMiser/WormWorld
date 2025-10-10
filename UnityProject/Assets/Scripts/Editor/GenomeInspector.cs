using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using UnityEditor;
using UnityEngine;
using WormWorld.Genome;

namespace WormWorld.EditorTools
{
    /// <summary>
    /// Custom inspector for genome JSONL <see cref="TextAsset"/> files providing a friendly summary view.
    /// </summary>
    [CustomEditor(typeof(TextAsset))]
    public class GenomeInspector : UnityEditor.Editor
    {
        private static readonly (string Label, Func<Genome, string?> Accessor)[] ComplexSections =
        {
            ("Metadata", genome => genome.MetadataJson),
            ("Body", genome => genome.BodyJson),
            ("Brain", genome => genome.BrainJson),
            ("Senses", genome => genome.SensesJson),
            ("Reproduction", genome => genome.ReproductionJson),
            ("Muscles", genome => genome.MusclesJson),
            ("Pheromones", genome => genome.PheromonesJson),
            ("Nerves", genome => genome.NervesJson),
            ("Energy", genome => genome.EnergyJson),
            ("Fitness Weights", genome => genome.FitnessJson)
        };

        private readonly Dictionary<string, bool> _foldoutStates = new Dictionary<string, bool>();
        private readonly Dictionary<string, string> _prettyPrintCache = new Dictionary<string, string>();

        private List<Genome>? _loadedGenomes;
        private string? _loadError;
        private int _selectedIndex;
        private Vector2 _scrollPosition;
        private bool _initialized;

        /// <inheritdoc />
        public override void OnInspectorGUI()
        {
            var asset = (TextAsset)target;
            if (!IsGenomeAsset(asset))
            {
                DrawDefaultInspector();
                return;
            }

            EnsureLoaded();

            if (GUILayout.Button("Reload"))
            {
                LoadGenomes();
            }

            if (!string.IsNullOrEmpty(_loadError))
            {
                EditorGUILayout.HelpBox(_loadError, MessageType.Error);
                return;
            }

            if (_loadedGenomes == null || _loadedGenomes.Count == 0)
            {
                EditorGUILayout.HelpBox("No genomes found in JSONL file.", MessageType.Info);
                return;
            }

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                var labels = _loadedGenomes.Select((g, index) => $"{index + 1}: {g.Name ?? g.Id ?? "(unnamed)"}").ToArray();
                if (labels.Length > 1)
                {
                    _selectedIndex = EditorGUILayout.Popup("Genome", _selectedIndex, labels);
                }
                else
                {
                    _selectedIndex = 0;
                }

                _selectedIndex = Mathf.Clamp(_selectedIndex, 0, _loadedGenomes.Count - 1);
                var genome = _loadedGenomes[_selectedIndex];

                EditorGUILayout.LabelField("Version", genome.Version ?? string.Empty);
                EditorGUILayout.LabelField("ID", genome.Id ?? string.Empty);
                EditorGUILayout.LabelField("Name", genome.Name ?? string.Empty);
                EditorGUILayout.LabelField("Seed", genome.Seed.ToString(CultureInfo.InvariantCulture));

                DrawNeuralConstraints(genome);

                _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition, GUILayout.Height(300f));
                foreach (var section in ComplexSections)
                {
                    DrawJsonFoldout(section.Label, section.Accessor(genome));
                }

                EditorGUILayout.EndScrollView();
            }
        }

        private void EnsureLoaded()
        {
            if (_initialized)
            {
                return;
            }

            LoadGenomes();
            _initialized = true;
        }

        private void LoadGenomes()
        {
            _loadError = null;
            _loadedGenomes = null;
            _prettyPrintCache.Clear();
            _foldoutStates.Clear();

            var asset = (TextAsset)target;
            var assetPath = AssetDatabase.GetAssetPath(asset);
            if (string.IsNullOrEmpty(assetPath))
            {
                _loadError = "Unable to determine asset path.";
                return;
            }

            try
            {
                var fullPath = ResolveProjectRelativePath(assetPath);
                _loadedGenomes = GenomeIO.ReadFromJsonl(fullPath).ToList();
                _selectedIndex = 0;
            }
            catch (Exception ex)
            {
                _loadError = $"Failed to load genome JSONL: {ex.Message}";
            }
        }

        private static bool IsGenomeAsset(TextAsset asset)
        {
            var path = AssetDatabase.GetAssetPath(asset);
            return !string.IsNullOrEmpty(path) && path.EndsWith(".jsonl", StringComparison.OrdinalIgnoreCase);
        }

        private static string ResolveProjectRelativePath(string assetPath)
        {
            var projectRoot = Path.GetDirectoryName(Application.dataPath) ?? Application.dataPath;
            return Path.GetFullPath(Path.Combine(projectRoot, assetPath));
        }

        private void DrawJsonFoldout(string label, string? rawJson)
        {
            if (string.IsNullOrWhiteSpace(rawJson))
            {
                EditorGUILayout.LabelField(label, "(empty)");
                return;
            }

            if (!_foldoutStates.TryGetValue(label, out var foldout))
            {
                foldout = false;
            }

            foldout = EditorGUILayout.Foldout(foldout, label, true);
            _foldoutStates[label] = foldout;

            if (!foldout)
            {
                return;
            }

            using (new EditorGUI.IndentLevelScope())
            {
                var pretty = GetPrettyJson(rawJson);
                EditorGUILayout.TextArea(pretty, EditorStyles.textArea, GUILayout.MinHeight(80f));
            }
        }

        private void DrawNeuralConstraints(Genome genome)
        {
            try
            {
                var brainCellCount = TryExtractBrainCellCount(genome);
                if (brainCellCount.HasValue)
                {
                    var constraints = NeuralConstraints.Calculate(brainCellCount.Value);
                    using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                    {
                        EditorGUILayout.LabelField("Neural Constraints", EditorStyles.boldLabel);
                        EditorGUILayout.LabelField("Brain Cell Count", brainCellCount.Value.ToString(CultureInfo.InvariantCulture));
                        EditorGUILayout.LabelField("Max Hidden Nodes", constraints.MaxHiddenNodes.ToString(CultureInfo.InvariantCulture));
                        EditorGUILayout.LabelField("Max Layers", constraints.MaxLayers.ToString(CultureInfo.InvariantCulture));
                        EditorGUILayout.LabelField("Hidden Budget / Layer", constraints.HiddenBudgetPerNewLayer.ToString(CultureInfo.InvariantCulture));
                    }
                }
                else
                {
                    EditorGUILayout.HelpBox("Brain data missing brain_cell_count; unable to infer neural constraints.", MessageType.Info);
                }
            }
            catch (Exception ex)
            {
                EditorGUILayout.HelpBox($"Failed to compute neural constraints: {ex.Message}", MessageType.Warning);
            }
        }

        private static int? TryExtractBrainCellCount(Genome genome)
        {
            if (string.IsNullOrWhiteSpace(genome.BrainJson))
            {
                return null;
            }

            using var document = JsonDocument.Parse(genome.BrainJson);
            if (document.RootElement.TryGetProperty("brain_cell_count", out var countElement) &&
                countElement.ValueKind == JsonValueKind.Number)
            {
                return countElement.GetInt32();
            }

            return null;
        }

        private string GetPrettyJson(string rawJson)
        {
            if (_prettyPrintCache.TryGetValue(rawJson, out var cached))
            {
                return cached;
            }

            try
            {
                using var document = JsonDocument.Parse(rawJson);
                using var builder = new MemoryStream();
                using (var writer = new Utf8JsonWriter(builder, new JsonWriterOptions { Indented = true }))
                {
                    document.WriteTo(writer);
                }

                var pretty = Encoding.UTF8.GetString(builder.ToArray());
                _prettyPrintCache[rawJson] = pretty;
                return pretty;
            }
            catch (JsonException)
            {
                _prettyPrintCache[rawJson] = rawJson;
                return rawJson;
            }
        }
    }
}
