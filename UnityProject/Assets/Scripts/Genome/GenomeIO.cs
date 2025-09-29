using System;
using System.Collections.Generic;

namespace WormWorld.Genome
{
    /// <summary>
    /// IO helpers and schema validation hooks for genome interchange formats.
    /// </summary>
    public static class GenomeIO
    {
        /// <summary>
        /// Reads genomes from the canonical CSV format into strongly typed objects.
        /// </summary>
        /// <param name="csvPath">Filesystem path to the CSV.</param>
        /// <returns>List of genomes described by the CSV rows.</returns>
        public static IList<Genome> ReadFromCsv(string csvPath)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Writes genomes to a JSONL file in expanded form.
        /// </summary>
        /// <param name="jsonlPath">Filesystem path to create or overwrite.</param>
        /// <param name="genomes">Sequence of genomes to serialize.</param>
        public static void WriteToJsonl(string jsonlPath, IEnumerable<Genome> genomes)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Reads genomes from an expanded JSONL file.
        /// </summary>
        /// <param name="jsonlPath">Filesystem path to the JSONL payload.</param>
        /// <returns>Materialized genomes.</returns>
        public static IList<Genome> ReadFromJsonl(string jsonlPath)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Validates a genome against the JSON Schema definition.
        /// </summary>
        /// <param name="genome">Genome to validate.</param>
        public static void ValidateWithSchema(Genome genome)
        {
            throw new NotImplementedException();
        }
    }
}