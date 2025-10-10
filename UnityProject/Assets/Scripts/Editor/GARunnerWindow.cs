using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEditor;
using UnityEngine;
using WormWorld.GA;
using WormWorld.Genome;

namespace WormWorld.EditorTools
{
    /// <summary>
    /// Editor window that evolves genomes offline using schema-aware GA operators.
    /// </summary>
    public class GARunnerWindow : EditorWindow
    {
        private const string MenuPath = "WormWorld/GA/Offline Evolve";
        private const string DefaultOutputRelative = "Data/genomes/out";

        private GAConfig? _config;
        private DefaultAsset? _inputCsvAsset;
        private string? _inputCsvPath;
        private DefaultAsset? _outputFolderAsset;
        private string? _outputFolderPath;
        private Vector2 _scrollPosition;
        private string? _statusMessage;

        /// <summary>
        /// Opens the GA runner window.
        /// </summary>
        [MenuItem(MenuPath)]
        public static void ShowWindow()
        {
            var window = GetWindow<GARunnerWindow>();
            window.titleContent = new GUIContent("Offline GA");
            window.minSize = new Vector2(460f, 340f);
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Offline Genome Evolution", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            _config = (GAConfig?)EditorGUILayout.ObjectField("GA Config", _config, typeof(GAConfig), false);

            EditorGUILayout.Space();

            EditorGUI.BeginChangeCheck();
            _inputCsvAsset = (DefaultAsset?)EditorGUILayout.ObjectField("Input CSV", _inputCsvAsset, typeof(DefaultAsset), false);
            if (EditorGUI.EndChangeCheck())
            {
                _inputCsvPath = ResolveAssetPath(_inputCsvAsset);
            }

            EditorGUI.BeginChangeCheck();
            _outputFolderAsset = (DefaultAsset?)EditorGUILayout.ObjectField("Output Folder", _outputFolderAsset, typeof(DefaultAsset), false);
            if (EditorGUI.EndChangeCheck())
            {
                _outputFolderPath = ResolveAssetPath(_outputFolderAsset);
            }

            if (!string.IsNullOrEmpty(_inputCsvPath))
            {
                EditorGUILayout.LabelField("Input", GetDisplayPath(_inputCsvPath));
            }

            if (!string.IsNullOrEmpty(_outputFolderPath))
            {
                EditorGUILayout.LabelField("Output", GetDisplayPath(_outputFolderPath));
            }

            EditorGUILayout.Space();

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Select CSV"))
                {
                    var selected = EditorUtility.OpenFilePanel("Select genome CSV", GetProjectRootPath(), "csv");
                    if (!string.IsNullOrEmpty(selected))
                    {
                        _inputCsvPath = selected;
                        _inputCsvAsset = LoadAssetFromPath(selected);
                    }
                }

                if (GUILayout.Button("Select Output"))
                {
                    var selected = EditorUtility.OpenFolderPanel("Select Output Folder", GetDefaultOutputPath(), string.Empty);
                    if (!string.IsNullOrEmpty(selected))
                    {
                        _outputFolderPath = selected;
                        _outputFolderAsset = LoadAssetFromPath(selected);
                    }
                }
            }

            EditorGUILayout.Space();

            using (new EditorGUI.DisabledScope(_config == null || string.IsNullOrEmpty(_inputCsvPath)))
            {
                if (GUILayout.Button("Run Evolution"))
                {
                    RunEvolution();
                }
            }

            if (!string.IsNullOrEmpty(_statusMessage))
            {
                EditorGUILayout.Space();
                EditorGUILayout.HelpBox(_statusMessage, MessageType.Info);
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Config Preview", EditorStyles.boldLabel);

            if (_config != null)
            {
                _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);
                EditorGUILayout.LabelField("Seed", _config.Seed.ToString(CultureInfo.InvariantCulture));
                EditorGUILayout.LabelField("Generations", _config.Generations.ToString(CultureInfo.InvariantCulture));
                EditorGUILayout.LabelField("Tournament Size", _config.TournamentSize.ToString(CultureInfo.InvariantCulture));
                EditorGUILayout.Space();
                EditorGUILayout.LabelField("Mutation Rates", EditorStyles.boldLabel);
                EditorGUILayout.LabelField("Muscle", _config.MuscleMutationRate.ToString("F2", CultureInfo.InvariantCulture));
                EditorGUILayout.LabelField("Hidden Nodes", _config.HiddenNodeMutationRate.ToString("F2", CultureInfo.InvariantCulture));
                EditorGUILayout.LabelField("Vision", _config.VisionMutationRate.ToString("F2", CultureInfo.InvariantCulture));
                EditorGUILayout.LabelField("Pheromone", _config.PheromoneToggleProbability.ToString("F2", CultureInfo.InvariantCulture));
                EditorGUILayout.LabelField("Material", _config.MaterialMutationRate.ToString("F2", CultureInfo.InvariantCulture));
                EditorGUILayout.EndScrollView();
            }
            else
            {
                EditorGUILayout.HelpBox("Assign a GAConfig asset to review parameters.", MessageType.Info);
            }
        }

        private void RunEvolution()
        {
            if (_config == null)
            {
                _statusMessage = "Assign a GAConfig asset before running.";
                return;
            }

            if (string.IsNullOrEmpty(_inputCsvPath) || !File.Exists(_inputCsvPath))
            {
                _statusMessage = "Select a valid input CSV containing genomes.";
                return;
            }

            var outputDirectory = string.IsNullOrEmpty(_outputFolderPath)
                ? GetDefaultOutputPath()
                : _outputFolderPath!;

            Directory.CreateDirectory(outputDirectory);

            try
            {
                var genomes = GenomeIO.ReadFromCsv(_inputCsvPath!);
                if (genomes.Count == 0)
                {
                    _statusMessage = "Input CSV did not contain any genomes.";
                    return;
                }

                var members = new List<PopulationMember>();
                foreach (var genome in genomes)
                {
                    var clone = GenomeCloneUtility.Clone(genome);
                    var fitness = genome.PreEvalFitness ?? 0.0;
                    clone.PreEvalFitness = fitness;
                    members.Add(new PopulationMember(clone, fitness));
                }

                var population = new Population(members, _config.Seed);

                for (var generation = 0; generation < _config.Generations; generation++)
                {
                    var nextMembers = new List<PopulationMember>(population.Count);
                    for (var index = 0; index < population.Count; index++)
                    {
                        var parentARng = population.CreateRng(generation, index, 11);
                        var parentBRng = population.CreateRng(generation, index, 23);
                        var crossoverRng = population.CreateRng(generation, index, 37);
                        var mutationRng = population.CreateRng(generation, index, 53);

                        var parentA = Selection.Tournament(population.Members, _config.TournamentSize, parentARng);
                        var parentB = Selection.Tournament(population.Members, _config.TournamentSize, parentBRng);

                        var child = Crossover.Combine(parentA.Genome, parentB.Genome, crossoverRng);
                        child = Mutation.Apply(child, _config, mutationRng);

                        var childFitness = (parentA.Fitness + parentB.Fitness) / 2.0;
                        child.PreEvalFitness = childFitness;

                        nextMembers.Add(new PopulationMember(child, childFitness));
                    }

                    population = new Population(nextMembers, population.Seed);
                }

                var outputGenomes = new List<Genome.Genome>();
                foreach (var member in population.Members)
                {
                    outputGenomes.Add(member.Genome);
                }

                var fileName = $"ga_{_config.Seed}_gen{_config.Generations}_{DateTime.UtcNow:yyyyMMddHHmmss}.csv";
                var outputPath = Path.Combine(outputDirectory, fileName);
                GenomeIO.WriteToCsv(outputPath, outputGenomes);

                _statusMessage = $"Evolution complete. Wrote {outputGenomes.Count} genomes to {GetDisplayPath(outputPath)}";
            }
            catch (Exception ex)
            {
                _statusMessage = $"Evolution failed: {ex.Message}";
            }
        }

        private static string? ResolveAssetPath(DefaultAsset? asset)
        {
            if (asset == null)
            {
                return null;
            }

            var relative = AssetDatabase.GetAssetPath(asset);
            return string.IsNullOrEmpty(relative) ? null : ResolveProjectRelativePath(relative);
        }

        private static DefaultAsset? LoadAssetFromPath(string absolutePath)
        {
            var projectRelative = ToProjectRelativePath(absolutePath);
            return string.IsNullOrEmpty(projectRelative)
                ? null
                : AssetDatabase.LoadAssetAtPath<DefaultAsset>(projectRelative);
        }

        private static string GetProjectRootPath()
        {
            return Directory.GetCurrentDirectory();
        }

        private static string GetDefaultOutputPath()
        {
            var projectRoot = GetProjectRootPath();
            return Path.Combine(projectRoot, DefaultOutputRelative);
        }

        private static string GetDisplayPath(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return string.Empty;
            }

            var projectRoot = GetProjectRootPath();
            return path.StartsWith(projectRoot, StringComparison.Ordinal)
                ? path.Substring(projectRoot.Length + 1)
                : path;
        }

        private static string? ResolveProjectRelativePath(string assetPath)
        {
            if (string.IsNullOrEmpty(assetPath))
            {
                return null;
            }

            if (!assetPath.StartsWith("Assets", StringComparison.Ordinal))
            {
                return null;
            }

            var projectRoot = GetProjectRootPath();
            return Path.Combine(projectRoot, assetPath);
        }

        private static string? ToProjectRelativePath(string absolutePath)
        {
            var projectRoot = GetProjectRootPath();
            if (!absolutePath.StartsWith(projectRoot, StringComparison.Ordinal))
            {
                return null;
            }

            var relative = absolutePath.Substring(projectRoot.Length);
            if (relative.StartsWith(Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal))
            {
                relative = relative.Substring(1);
            }

            return string.IsNullOrEmpty(relative) ? null : relative.Replace(Path.DirectorySeparatorChar, '/');
        }
    }
}
