using Microsoft.CodeAnalysis;

namespace WormWorld.Analyzers;

internal static class Diagnostics
{
    public const string RandomOutsideRngServiceId = "WW001";
    public const string GenomeSchemaVersionMismatchId = "WW002";
    public const string GenomeFieldNotInSchemaId = "WW003";

    public static readonly DiagnosticDescriptor RandomOutsideRngService = new(
        RandomOutsideRngServiceId,
        "Use RngService for randomness",
        "Randomness must be accessed through RngService for determinism",
        "Determinism",
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Calls to UnityEngine.Random or System.Random should be routed through the deterministic RngService wrapper.");

    public static readonly DiagnosticDescriptor GenomeSchemaVersionMismatch = new(
        GenomeSchemaVersionMismatchId,
        "Genome schema version mismatch",
        "Genome.SCHEMA_VERSION must match the schema version '{0}'",
        "Schema",
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Keep the Genome class in sync with the published schema version in Data/schemas/genome.schema.json.");

    public static readonly DiagnosticDescriptor GenomeFieldNotInSchema = new(
        GenomeFieldNotInSchemaId,
        "Genome member missing from schema",
        "Public member '{0}' is not declared in genome.schema.json properties",
        "Schema",
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Expose only members that are backed by schema properties or update the schema alongside Genome.");
}
