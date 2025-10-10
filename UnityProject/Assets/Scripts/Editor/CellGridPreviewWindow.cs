using System;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using WormWorld.Genome;
using WormWorld.Runtime;

namespace WormWorld.EditorTools
{
    public sealed class CellGridPreviewWindow : EditorWindow
    {
        private TextAsset _genomeAsset;
        private string _jsonlPath;
        private CellPhysicsConfig _config;
        private Genome _loadedGenome;
        private CellGridBuilder.BuildResult _preview;
        private bool _hasPreview;

        [MenuItem("WormWorld/Genomes/Preview Cell Grid", priority = 20)]
        public static void ShowWindow()
        {
            var window = GetWindow<CellGridPreviewWindow>();
            window.titleContent = new GUIContent("Cell Grid Preview");
            window.Show();
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Genome Source", EditorStyles.boldLabel);
            _genomeAsset = (TextAsset)EditorGUILayout.ObjectField("JSONL Asset", _genomeAsset, typeof(TextAsset), false);
            _jsonlPath = EditorGUILayout.TextField("JSONL Path", _jsonlPath);
            _config = (CellPhysicsConfig)EditorGUILayout.ObjectField("Physics Config", _config, typeof(CellPhysicsConfig), false);

            EditorGUILayout.Space();

            using (new EditorGUILayout.HorizontalScope())
            {
                GUI.enabled = _config != null;
                if (GUILayout.Button("Build Preview"))
                {
                    BuildPreview();
                }

                GUI.enabled = _hasPreview;
                if (GUILayout.Button("Clear"))
                {
                    ClearPreview();
                }

                GUI.enabled = true;
            }

            EditorGUILayout.Space();

            if (_hasPreview && _loadedGenome != null)
            {
                EditorGUILayout.LabelField("Preview Info", EditorStyles.boldLabel);
                EditorGUILayout.LabelField("ID", _loadedGenome.Id ?? "(unnamed)");
                EditorGUILayout.LabelField("Name", _loadedGenome.Name ?? "(unnamed)");
                EditorGUILayout.LabelField("Cells", _preview.Cells?.Count.ToString() ?? "0");

                if (GUILayout.Button("Create Prefab"))
                {
                    SavePrefab();
                }
            }
            else
            {
                EditorGUILayout.HelpBox("Load a genome and build the preview to inspect its cell layout.", MessageType.Info);
            }
        }

        private void BuildPreview()
        {
            try
            {
                ClearPreview();
                _loadedGenome = LoadGenome();
                if (_loadedGenome == null)
                {
                    return;
                }

                var container = new GameObject("GenomePreviewContainer")
                {
                    hideFlags = HideFlags.HideAndDontSave
                };

                _preview = CellGridBuilder.Build(container, _loadedGenome, _config);
                _preview.Root.hideFlags = HideFlags.HideAndDontSave;
                foreach (var cell in _preview.Cells.Values)
                {
                    if (cell.Body != null)
                    {
                        cell.Body.simulated = false;
                    }
                }

                SceneView.RepaintAll();
                _hasPreview = true;
            }
            catch (Exception ex)
            {
                ClearPreview();
                EditorUtility.DisplayDialog("Preview Error", ex.Message, "OK");
            }
        }

        private Genome LoadGenome()
        {
            var jsonLine = ExtractGenomeLine();
            if (string.IsNullOrEmpty(jsonLine))
            {
                EditorUtility.DisplayDialog("Genome Preview", "Unable to locate a JSONL genome entry.", "OK");
                return null;
            }

            var token = JToken.Parse(jsonLine);
            return new Genome
            {
                Version = token.Value<string>("version") ?? "v0",
                Id = token.Value<string>("id") ?? "preview",
                Name = token.Value<string>("name") ?? "Preview",
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
                FitnessJson = token["fitness_weights"]?.ToString(Newtonsoft.Json.Formatting.None) ?? "{}"
            };
        }

        private string ExtractGenomeLine()
        {
            if (_genomeAsset != null && !string.IsNullOrEmpty(_genomeAsset.text))
            {
                using var reader = new StringReader(_genomeAsset.text);
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    if (!string.IsNullOrWhiteSpace(line))
                    {
                        return line.Trim();
                    }
                }
            }

            if (!string.IsNullOrEmpty(_jsonlPath))
            {
                var path = _jsonlPath;
                if (!Path.IsPathRooted(path))
                {
                    var projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
                    path = Path.GetFullPath(Path.Combine(projectRoot, path));
                }

                if (File.Exists(path))
                {
                    return File.ReadLines(path).FirstOrDefault(line => !string.IsNullOrWhiteSpace(line))?.Trim() ?? string.Empty;
                }
            }

            return string.Empty;
        }

        private void SavePrefab()
        {
            if (!_hasPreview || _preview.Root == null)
            {
                return;
            }

            const string rootFolder = "Assets/Generated";
            const string creatureFolder = rootFolder + "/Creatures";
            if (!AssetDatabase.IsValidFolder(rootFolder))
            {
                AssetDatabase.CreateFolder("Assets", "Generated");
            }

            if (!AssetDatabase.IsValidFolder(creatureFolder))
            {
                AssetDatabase.CreateFolder(rootFolder, "Creatures");
            }

            var id = string.IsNullOrWhiteSpace(_loadedGenome?.Id) ? "creature" : _loadedGenome.Id;
            var path = AssetDatabase.GenerateUniqueAssetPath($"{creatureFolder}/{id}.prefab");

            try
            {
                SetSimulationState(true);
                PrefabUtility.SaveAsPrefabAsset(_preview.Root, path);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                EditorUtility.DisplayDialog("Prefab Created", $"Saved preview prefab to {path}.", "OK");
            }
            catch (Exception ex)
            {
                EditorUtility.DisplayDialog("Prefab Error", ex.Message, "OK");
            }
            finally
            {
                SetSimulationState(false);
            }
        }

        private void SetSimulationState(bool enabled)
        {
            if (!_hasPreview || _preview.Cells == null)
            {
                return;
            }

            foreach (var cell in _preview.Cells.Values)
            {
                if (cell.Body != null)
                {
                    cell.Body.simulated = enabled;
                }
            }
        }

        private void ClearPreview()
        {
            if (_preview.Root != null)
            {
                DestroyImmediate(_preview.Root.transform.parent != null ? _preview.Root.transform.parent.gameObject : _preview.Root);
            }

            _preview = default;
            _loadedGenome = null;
            _hasPreview = false;
            SceneView.RepaintAll();
        }

        private void OnDisable()
        {
            ClearPreview();
        }
    }
}
