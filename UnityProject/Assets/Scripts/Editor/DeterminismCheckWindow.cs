using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEngine;
using WormWorld.Genome;

namespace WormWorld.EditorTools
{
    /// <summary>
    /// Utility window that verifies canonical serialization remains deterministic for a genome JSONL file.
    /// </summary>
    public class DeterminismCheckWindow : EditorWindow
    {
        private const string MenuPath = "WormWorld/QA/Determinism Check";

        private string? _jsonlPath;
        private string _seedText = string.Empty;
        private string? _resultMessage;
        private string? _diffDetails;
        private bool _success;
        private Vector2 _scrollPosition;

        /// <summary>
        /// Opens the determinism check window.
        /// </summary>
        [MenuItem(MenuPath)]
        public static void ShowWindow()
        {
            var window = GetWindow<DeterminismCheckWindow>();
            window.titleContent = new GUIContent("Determinism Check");
            window.minSize = new Vector2(420f, 300f);
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Determinism Verification", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("JSONL Path", GUILayout.Width(80f));
            EditorGUILayout.SelectableLabel(_jsonlPath ?? "", EditorStyles.textField, GUILayout.Height(EditorGUIUtility.singleLineHeight));
            if (GUILayout.Button("Browse", GUILayout.Width(80f)))
            {
                var selected = EditorUtility.OpenFilePanel("Select genome JSONL", GetProjectRootPath(), "jsonl");
                if (!string.IsNullOrEmpty(selected))
                {
                    _jsonlPath = selected;
                }
            }

            EditorGUILayout.EndHorizontal();

            _seedText = EditorGUILayout.TextField("Seed Override", _seedText);

            using (new EditorGUI.DisabledScope(string.IsNullOrEmpty(_jsonlPath)))
            {
                if (GUILayout.Button("Run Check"))
                {
                    RunCheck();
                }
            }

            if (!string.IsNullOrEmpty(_resultMessage))
            {
                EditorGUILayout.HelpBox(_resultMessage, _success ? MessageType.Info : MessageType.Warning);
            }

            if (!string.IsNullOrEmpty(_diffDetails))
            {
                EditorGUILayout.LabelField("Differences", EditorStyles.boldLabel);
                _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);
                EditorGUILayout.TextArea(_diffDetails, GUILayout.MinHeight(120f));
                EditorGUILayout.EndScrollView();
            }
        }

        private void RunCheck()
        {
            _resultMessage = null;
            _diffDetails = null;
            _success = false;

            if (string.IsNullOrEmpty(_jsonlPath) || !File.Exists(_jsonlPath))
            {
                _resultMessage = "Select a valid JSONL file.";
                return;
            }

            if (!TryParseSeed(_seedText, out var seedOverride, out var seedError))
            {
                _resultMessage = seedError;
                return;
            }

            try
            {
                var genomesFirst = LoadGenomes(_jsonlPath, seedOverride);
                var genomesSecond = LoadGenomes(_jsonlPath, seedOverride);

                var canonicalFirst = genomesFirst.Select(GenomeIO.ToCanonicalJson).ToList();
                var canonicalSecond = genomesSecond.Select(GenomeIO.ToCanonicalJson).ToList();

                var hashFirst = ComputeHash(canonicalFirst);
                var hashSecond = ComputeHash(canonicalSecond);

                if (string.Equals(hashFirst, hashSecond, StringComparison.Ordinal))
                {
                    _success = true;
                    _resultMessage = $"Deterministic: {hashFirst}";
                }
                else
                {
                    _success = false;
                    _resultMessage = $"Hashes differ: {hashFirst} vs {hashSecond}";
                    _diffDetails = BuildDiff(canonicalFirst, canonicalSecond);
                }
            }
            catch (Exception ex)
            {
                _resultMessage = $"Determinism check failed: {ex.Message}";
            }
        }

        private static List<Genome> LoadGenomes(string path, ulong? seedOverride)
        {
            var genomes = GenomeIO.ReadFromJsonl(path).ToList();
            if (seedOverride.HasValue)
            {
                foreach (var genome in genomes)
                {
                    genome.Seed = seedOverride.Value;
                }
            }

            return genomes;
        }

        private static bool TryParseSeed(string input, out ulong? seed, out string? error)
        {
            seed = null;
            error = null;

            if (string.IsNullOrWhiteSpace(input))
            {
                return true;
            }

            if (ulong.TryParse(input, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
            {
                seed = parsed;
                return true;
            }

            error = "Seed must be an unsigned integer.";
            return false;
        }

        private static string ComputeHash(IEnumerable<string> canonicalJson)
        {
            using var sha = SHA256.Create();
            var builder = new StringBuilder();
            foreach (var line in canonicalJson)
            {
                var normalized = JsonCompat.Normalize(line);
                builder.AppendLine(normalized);
            }

            var bytes = Encoding.UTF8.GetBytes(builder.ToString());
            var hash = sha.ComputeHash(bytes);
            return BitConverter.ToString(hash).Replace("-", string.Empty, StringComparison.Ordinal);
        }

        private static string BuildDiff(IReadOnlyList<string> first, IReadOnlyList<string> second)
        {
            var builder = new StringBuilder();
            if (first.Count != second.Count)
            {
                builder.AppendLine($"Genome count differs: {first.Count} vs {second.Count}");
            }

            var max = Math.Max(first.Count, second.Count);
            for (var i = 0; i < max; i++)
            {
                if (i >= first.Count)
                {
                    builder.AppendLine($"Additional genome in second set at index {i}:");
                    builder.AppendLine(second[i]);
                    break;
                }

                if (i >= second.Count)
                {
                    builder.AppendLine($"Additional genome in first set at index {i}:");
                    builder.AppendLine(first[i]);
                    break;
                }

                if (!string.Equals(first[i], second[i], StringComparison.Ordinal))
                {
                    builder.AppendLine($"Difference at index {i}:");
                    builder.AppendLine("First:");
                    builder.AppendLine(first[i]);
                    builder.AppendLine("Second:");
                    builder.AppendLine(second[i]);
                    break;
                }
            }

            return builder.Length == 0 ? "Hashes differ but canonical JSON matched." : builder.ToString();
        }

        private static string GetProjectRootPath()
        {
            return Path.GetDirectoryName(Application.dataPath) ?? Application.dataPath;
        }
    }
}
