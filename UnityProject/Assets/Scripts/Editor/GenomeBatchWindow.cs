using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEditor;
using UnityEngine;
using WormWorld.Genome;

namespace WormWorld.EditorTools
{
    /// <summary>
    /// Editor window that batch converts CSV genomes to JSONL and validates against the schema.
    /// </summary>
    public class GenomeBatchWindow : EditorWindow
    {
        private const string MenuPath = "WormWorld/Genomes/Batch Convert";
        private const string OutputRelativeDirectory = "Data/genomes/detailed";

        private DefaultAsset? _csvFolderAsset;
        private string? _csvFolderAbsolutePath;
        private readonly List<BatchEntry> _results = new List<BatchEntry>();
        private Vector2 _scrollPosition;
        private string? _statusMessage;

        /// <summary>
        /// Opens the batch conversion window.
        /// </summary>
        [MenuItem(MenuPath)]
        public static void ShowWindow()
        {
            var window = GetWindow<GenomeBatchWindow>();
            window.titleContent = new GUIContent("Genome Batch Convert");
            window.minSize = new Vector2(420f, 320f);
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Batch CSV → JSONL Conversion", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            EditorGUI.BeginChangeCheck();
            _csvFolderAsset = (DefaultAsset?)EditorGUILayout.ObjectField("CSV Folder", _csvFolderAsset, typeof(DefaultAsset), false);
            if (EditorGUI.EndChangeCheck())
            {
                if (_csvFolderAsset != null)
                {
                    var relativePath = AssetDatabase.GetAssetPath(_csvFolderAsset);
                    _csvFolderAbsolutePath = string.IsNullOrEmpty(relativePath)
                        ? null
                        : ResolveProjectRelativePath(relativePath);
                }
                else
                {
                    _csvFolderAbsolutePath = null;
                }
            }

            if (!string.IsNullOrEmpty(_csvFolderAbsolutePath))
            {
                EditorGUILayout.LabelField("Folder", GetDisplayPath(_csvFolderAbsolutePath));
            }

            EditorGUILayout.Space();

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Select Folder"))
                {
                    var selected = EditorUtility.OpenFolderPanel("Select CSV Folder", GetProjectRootPath(), string.Empty);
                    if (!string.IsNullOrEmpty(selected))
                    {
                        _csvFolderAbsolutePath = selected;
                        var relative = ToProjectRelativePath(selected);
                        _csvFolderAsset = !string.IsNullOrEmpty(relative)
                            ? AssetDatabase.LoadAssetAtPath<DefaultAsset>(relative)
                            : null;
                    }
                }

                using (new EditorGUI.DisabledScope(string.IsNullOrEmpty(_csvFolderAbsolutePath)))
                {
                    if (GUILayout.Button("Run Batch"))
                    {
                        RunBatch();
                    }
                }
            }

            if (!string.IsNullOrEmpty(_statusMessage))
            {
                EditorGUILayout.HelpBox(_statusMessage, MessageType.Info);
            }

            if (_results.Count > 0)
            {
                EditorGUILayout.Space();
                EditorGUILayout.LabelField("Summary", EditorStyles.boldLabel);
                _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);

                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    foreach (var entry in _results)
                    {
                        using (new EditorGUILayout.VerticalScope())
                        {
                            EditorGUILayout.LabelField("CSV", GetDisplayPath(entry.CsvPath));
                            EditorGUILayout.LabelField("JSONL", GetDisplayPath(entry.JsonlPath));
                            EditorGUILayout.LabelField("Genomes", entry.GenomeCount.ToString(CultureInfo.InvariantCulture));
                            var message = entry.ErrorMessage ?? "Success";
                            var messageType = entry.ErrorMessage == null ? MessageType.Info : MessageType.Error;
                            EditorGUILayout.HelpBox(message, messageType);
                        }

                        EditorGUILayout.Space();
                    }
                }

                EditorGUILayout.EndScrollView();

                if (GUILayout.Button("Open Output Folder"))
                {
                    var outputPath = GetOutputDirectory();
                    if (Directory.Exists(outputPath))
                    {
                        EditorUtility.RevealInFinder(outputPath);
                    }
                    else
                    {
                        EditorUtility.DisplayDialog("Output Folder Missing", outputPath, "OK");
                    }
                }
            }
        }

        private void RunBatch()
        {
            _results.Clear();
            _statusMessage = null;

            if (string.IsNullOrEmpty(_csvFolderAbsolutePath))
            {
                _statusMessage = "Select a folder containing CSV genomes.";
                return;
            }

            var absoluteInput = _csvFolderAbsolutePath;
            if (string.IsNullOrEmpty(absoluteInput) || !Directory.Exists(absoluteInput))
            {
                _statusMessage = $"Input folder not found: {GetDisplayPath(_csvFolderAbsolutePath)}";
                return;
            }

            var csvFiles = Directory.GetFiles(absoluteInput, "*.csv", SearchOption.TopDirectoryOnly);
            if (csvFiles.Length == 0)
            {
                _statusMessage = "No CSV files found in the selected folder.";
                return;
            }

            var outputDirectory = GetOutputDirectory();
            Directory.CreateDirectory(outputDirectory);

            var successCount = 0;
            foreach (var file in csvFiles)
            {
                var entry = new BatchEntry
                {
                    CsvPath = file,
                    GenomeCount = 0
                };

                try
                {
                    var genomes = GenomeIO.ReadFromCsv(file);
                    foreach (var genome in genomes)
                    {
                        GenomeIO.ValidateWithSchema(genome);
                    }

                    entry.GenomeCount = genomes.Count;

                    var fileName = Path.GetFileNameWithoutExtension(file) + ".jsonl";
                    var destination = Path.Combine(outputDirectory, fileName);
                    GenomeIO.WriteToJsonl(destination, genomes);
                    entry.JsonlPath = destination;
                    successCount++;
                }
                catch (Exception ex)
                {
                    entry.ErrorMessage = ex.Message;
                }

                _results.Add(entry);
            }

            _statusMessage = $"Processed {csvFiles.Length} file(s); {successCount} succeeded.";
        }

        private static string GetProjectRootPath()
        {
            return Path.GetDirectoryName(Application.dataPath) ?? Application.dataPath;
        }

        private static string ResolveProjectRelativePath(string projectRelativePath)
        {
            var root = GetProjectRootPath();
            return Path.GetFullPath(Path.Combine(root, projectRelativePath));
        }

        private static string? ToProjectRelativePath(string absolutePath)
        {
            var root = GetProjectRootPath();
            if (!absolutePath.StartsWith(root, StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            var relative = absolutePath.Substring(root.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            return string.IsNullOrEmpty(relative) ? null : relative.Replace(Path.DirectorySeparatorChar, '/');
        }

        private static string GetDisplayPath(string? absolutePath)
        {
            if (string.IsNullOrEmpty(absolutePath))
            {
                return "(none)";
            }

            var root = GetProjectRootPath();
            if (absolutePath.StartsWith(root, StringComparison.OrdinalIgnoreCase))
            {
                var relative = absolutePath.Substring(root.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                if (!string.IsNullOrEmpty(relative))
                {
                    return relative.Replace(Path.DirectorySeparatorChar, '/');
                }
            }

            return absolutePath;
        }

        private static string GetOutputDirectory()
        {
            var root = GetProjectRootPath();
            return Path.GetFullPath(Path.Combine(root, OutputRelativeDirectory));
        }

        private sealed class BatchEntry
        {
            public string CsvPath = string.Empty;
            public string? JsonlPath;
            public int GenomeCount;
            public string? ErrorMessage;
        }
    }
}