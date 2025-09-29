using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using WormWorld.Genome;

namespace WormWorld.Tools.CsvJsonl
{
    internal static class Program
    {
        private static int Main(string[] args)
        {
            try
            {
                return Run(args);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex.Message);
                return 1;
            }
        }

        private static int Run(string[] args)
        {
            if (args == null || args.Length == 0)
            {
                PrintUsage();
                return 1;
            }

            string? csvPath = null;
            string? jsonlPath = null;
            string? outputPath = null;
            ulong? seedOverride = null;

            for (var i = 0; i < args.Length; i++)
            {
                var argument = args[i];
                switch (argument)
                {
                    case "--csv":
                        csvPath = RequireValue(args, ref i, "--csv");
                        break;
                    case "--jsonl":
                        jsonlPath = RequireValue(args, ref i, "--jsonl");
                        break;
                    case "--out":
                        outputPath = RequireValue(args, ref i, "--out");
                        break;
                    case "--seed":
                        var seedValue = RequireValue(args, ref i, "--seed");
                        seedOverride = ulong.Parse(seedValue, NumberStyles.Integer, CultureInfo.InvariantCulture);
                        break;
                    case "--help":
                    case "-h":
                        PrintUsage();
                        return 0;
                    default:
                        Console.Error.WriteLine($"Unrecognized argument '{argument}'.");
                        PrintUsage();
                        return 1;
                }
            }

            if (!string.IsNullOrEmpty(csvPath) == !string.IsNullOrEmpty(jsonlPath))
            {
                Console.Error.WriteLine("Specify exactly one of --csv or --jsonl.");
                PrintUsage();
                return 1;
            }

            if (string.IsNullOrEmpty(outputPath))
            {
                Console.Error.WriteLine("Missing required --out path.");
                PrintUsage();
                return 1;
            }

            if (!string.IsNullOrEmpty(csvPath))
            {
                csvPath = Path.GetFullPath(csvPath);
                outputPath = Path.GetFullPath(outputPath);
                var genomes = GenomeIO.ReadFromCsv(csvPath);
                if (seedOverride.HasValue)
                {
                    foreach (var genome in genomes)
                    {
                        genome.Seed = seedOverride.Value;
                    }
                }

                var errors = ValidateGenomes(genomes, 2, "row");
                if (errors.Count > 0)
                {
                    foreach (var error in errors)
                    {
                        Console.Error.WriteLine(error);
                    }

                    return 1;
                }

                GenomeIO.WriteToJsonl(outputPath, genomes);
                Console.WriteLine($"Wrote {genomes.Count} genome(s) to {outputPath}.");
                return 0;
            }
            else
            {
                jsonlPath = Path.GetFullPath(jsonlPath!);
                outputPath = Path.GetFullPath(outputPath);
                var genomes = GenomeIO.ReadFromJsonl(jsonlPath);
                if (seedOverride.HasValue)
                {
                    foreach (var genome in genomes)
                    {
                        genome.Seed = seedOverride.Value;
                    }
                }

                var errors = ValidateGenomes(genomes, 1, "line");
                if (errors.Count > 0)
                {
                    foreach (var error in errors)
                    {
                        Console.Error.WriteLine(error);
                    }

                    return 1;
                }

                GenomeIO.WriteToCsv(outputPath, genomes);
                Console.WriteLine($"Wrote {genomes.Count} genome(s) to {outputPath}.");
                return 0;
            }
        }

        private static List<string> ValidateGenomes(IList<Genome> genomes, int indexOffset, string label)
        {
            var errors = new List<string>();
            for (var i = 0; i < genomes.Count; i++)
            {
                try
                {
                    GenomeIO.ValidateWithSchema(genomes[i]);
                }
                catch (GenomeIO.SchemaValidationException ex)
                {
                    foreach (var error in ex.Errors)
                    {
                        errors.Add($"{label} {indexOffset + i}: {error}");
                    }
                }
            }

            return errors;
        }

        private static string RequireValue(IReadOnlyList<string> args, ref int index, string flag)
        {
            if (index + 1 >= args.Count)
            {
                throw new ArgumentException($"Expected value after {flag}.");
            }

            index++;
            return args[index];
        }

        private static void PrintUsage()
        {
            Console.WriteLine("WormWorld CSV/JSONL utility");
            Console.WriteLine("Usage:");
            Console.WriteLine("  --csv <path>   --out <jsonl_path>   [--seed <override>]  Convert CSV to JSONL and validate");
            Console.WriteLine("  --jsonl <path> --out <csv_path>     [--seed <override>]  Convert JSONL to CSV");
            Console.WriteLine("Optional:");
            Console.WriteLine("  --seed <value> Override the genome seed for all rows.");
        }
    }
}