using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using NUnit.Framework;
using WormWorld.Genome;

namespace WormWorld.Tests.EditMode
{
    public class GenomeIoTests
    {
        private static readonly string RepoRoot = LocateRepoRoot();

        [SetUp]
        public void SetUp()
        {
            GenomeIO.SchemaPath = Path.Combine(RepoRoot, "Data", "schemas", "genome.schema.json");
        }

        [Test]
        public void CsvJsonlRoundTripPreservesGenomes()
        {
            var csvPath = Path.Combine(RepoRoot, "Data", "genomes", "genomes.csv");
            var jsonlTemp = Path.Combine(Path.GetTempPath(), $"wormworld_{Guid.NewGuid():N}.jsonl");
            var csvTemp = Path.Combine(Path.GetTempPath(), $"wormworld_{Guid.NewGuid():N}.csv");

            try
            {
                var originalGenomes = GenomeIO.ReadFromCsv(csvPath);
                Assert.That(originalGenomes, Is.Not.Empty, "Expected seed genome data.");

                GenomeIO.WriteToJsonl(jsonlTemp, originalGenomes);
                var expandedGenomes = GenomeIO.ReadFromJsonl(jsonlTemp);
                GenomeIO.WriteToCsv(csvTemp, expandedGenomes);

                var roundTrip = GenomeIO.ReadFromCsv(csvTemp);
                Assert.That(roundTrip.Count, Is.EqualTo(originalGenomes.Count));

                for (var i = 0; i < originalGenomes.Count; i++)
                {
                    AssertGenomesEqual(originalGenomes[i], roundTrip[i], i);
                }
            }
            finally
            {
                if (File.Exists(jsonlTemp))
                {
                    File.Delete(jsonlTemp);
                }

                if (File.Exists(csvTemp))
                {
                    File.Delete(csvTemp);
                }
            }
        }

        [Test]
        public void SchemaValidationHighlightsMissingMetadata()
        {
            var csvPath = Path.Combine(RepoRoot, "Data", "genomes", "genomes.csv");
            var genomes = GenomeIO.ReadFromCsv(csvPath);
            Assert.That(genomes, Is.Not.Empty);

            var invalid = CloneGenome(genomes[0]);
            invalid.MetadataJson = "{}"; // remove required rng_service

            var exception = Assert.Throws<GenomeIO.SchemaValidationException>(() => GenomeIO.ValidateWithSchema(invalid));
            Assert.That(exception, Is.Not.Null);
            Assert.That(exception!.Message, Does.Contain("rng_service"));
        }

        [Test]
        public void CanonicalJsonIsDeterministic()
        {
            var csvPath = Path.Combine(RepoRoot, "Data", "genomes", "genomes.csv");
            var firstParse = GenomeIO.ReadFromCsv(csvPath);
            var secondParse = GenomeIO.ReadFromCsv(csvPath);
            Assert.That(firstParse.Count, Is.EqualTo(secondParse.Count));

            for (var i = 0; i < firstParse.Count; i++)
            {
                var firstHash = ComputeHash(GenomeIO.ToCanonicalJson(firstParse[i]));
                var secondHash = ComputeHash(GenomeIO.ToCanonicalJson(secondParse[i]));
                Assert.That(secondHash, Is.EqualTo(firstHash), $"Genome index {i} produced differing canonical hashes.");
            }
        }

        private static void AssertGenomesEqual(Genome expected, Genome actual, int index)
        {
            Assert.That(actual.Version, Is.EqualTo(expected.Version), $"Version mismatch at genome {index}.");
            Assert.That(actual.Id, Is.EqualTo(expected.Id), $"Id mismatch at genome {index}.");
            Assert.That(actual.Name, Is.EqualTo(expected.Name), $"Name mismatch at genome {index}.");
            Assert.That(actual.Seed, Is.EqualTo(expected.Seed), $"Seed mismatch at genome {index}.");
            Assert.That(actual.MetadataJson, Is.EqualTo(expected.MetadataJson), $"Metadata mismatch at genome {index}.");
            Assert.That(actual.BodyJson, Is.EqualTo(expected.BodyJson), $"Body mismatch at genome {index}.");
            Assert.That(actual.BrainJson, Is.EqualTo(expected.BrainJson), $"Brain mismatch at genome {index}.");
            Assert.That(actual.SensesJson, Is.EqualTo(expected.SensesJson), $"Senses mismatch at genome {index}.");
            Assert.That(actual.ReproductionJson, Is.EqualTo(expected.ReproductionJson), $"Reproduction mismatch at genome {index}.");
            Assert.That(actual.MusclesJson, Is.EqualTo(expected.MusclesJson), $"Muscles mismatch at genome {index}.");
            Assert.That(actual.PheromonesJson, Is.EqualTo(expected.PheromonesJson), $"Pheromones mismatch at genome {index}.");
            Assert.That(actual.NervesJson, Is.EqualTo(expected.NervesJson), $"Nerves mismatch at genome {index}.");
            Assert.That(actual.EnergyJson, Is.EqualTo(expected.EnergyJson), $"Energy mismatch at genome {index}.");
            Assert.That(actual.FitnessJson, Is.EqualTo(expected.FitnessJson), $"Fitness mismatch at genome {index}.");
        }

        private static Genome CloneGenome(Genome source)
        {
            return new Genome
            {
                Version = source.Version,
                Id = source.Id,
                Name = source.Name,
                Seed = source.Seed,
                MetadataJson = source.MetadataJson,
                BodyJson = source.BodyJson,
                BrainJson = source.BrainJson,
                SensesJson = source.SensesJson,
                ReproductionJson = source.ReproductionJson,
                MusclesJson = source.MusclesJson,
                PheromonesJson = source.PheromonesJson,
                NervesJson = source.NervesJson,
                EnergyJson = source.EnergyJson,
                FitnessJson = source.FitnessJson
            };
        }

        private static string ComputeHash(string canonicalJson)
        {
            using var sha = SHA256.Create();
            var bytes = Encoding.UTF8.GetBytes(canonicalJson);
            var hash = sha.ComputeHash(bytes);
            return BitConverter.ToString(hash).Replace("-", string.Empty);
        }

        private static string LocateRepoRoot()
        {
            var directory = new DirectoryInfo(Directory.GetCurrentDirectory());
            while (directory != null)
            {
                if (File.Exists(Path.Combine(directory.FullName, "Docs", "genome-spec.md")))
                {
                    return directory.FullName;
                }

                directory = directory.Parent;
            }

            throw new DirectoryNotFoundException("Unable to locate WormWorld repository root.");
        }
    }
}
