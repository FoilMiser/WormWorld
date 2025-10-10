using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using WormWorld.GA;
using WormWorld.Genome;

namespace WormWorld.Tests.EditMode
{
    public class GATests
    {
        private static readonly string RepoRoot = LocateRepoRoot();

        [SetUp]
        public void SetUp()
        {
            GenomeIO.SchemaPath = Path.Combine(RepoRoot, "Data", "schemas", "genome.schema.json");
        }

        [Test]
        public void MutationProducesSchemaValidGenome()
        {
            var csvPath = Path.Combine(RepoRoot, "Data", "genomes", "genomes.csv");
            var genome = GenomeIO.ReadFromCsv(csvPath).First();

            var config = ScriptableObject.CreateInstance<GAConfig>();
            config.MuscleMutationRate = 1f;
            config.HiddenNodeMutationRate = 1f;
            config.VisionMutationRate = 1f;
            config.PheromoneToggleProbability = 1f;
            config.MaterialMutationRate = 1f;
            config.StrengthFactorJitter = 0.4f;
            config.MaxStrengthFactor = 4f;
            config.VisionTradeoffStep = 0.1f;
            config.VisionEnergyStep = 0.1f;
            config.VisionClarityStep = 0.05f;
            config.PheromoneEnableValue = 0.6f;
            config.PheromoneDisableThreshold = 0.05f;
            config.MaterialNudge = 0.1f;

            var rng = DeterministicRng.Create(42UL, 0);
            var mutated = Mutation.Apply(genome, config, rng);

            Assert.DoesNotThrow(() => GenomeIO.ValidateWithSchema(mutated));
        }

        [Test]
        public void MutationIsDeterministicPerSeed()
        {
            var csvPath = Path.Combine(RepoRoot, "Data", "genomes", "genomes.csv");
            var genome = GenomeIO.ReadFromCsv(csvPath).First();

            var config = ScriptableObject.CreateInstance<GAConfig>();

            var first = Mutation.Apply(genome, config, DeterministicRng.Create(999UL, 1));
            var second = Mutation.Apply(genome, config, DeterministicRng.Create(999UL, 1));
            var third = Mutation.Apply(genome, config, DeterministicRng.Create(1000UL, 1));

            Assert.That(GenomeIO.ToCanonicalJson(first), Is.EqualTo(GenomeIO.ToCanonicalJson(second)));
            Assert.That(GenomeIO.ToCanonicalJson(first), Is.Not.EqualTo(GenomeIO.ToCanonicalJson(third)));
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

            throw new DirectoryNotFoundException("Unable to locate repository root.");
        }
    }
}
