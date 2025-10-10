using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace WormWorld.Analyzers;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class GenomeSchemaAnalyzer : DiagnosticAnalyzer
{
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(
            Diagnostics.GenomeSchemaVersionMismatch,
            Diagnostics.GenomeFieldNotInSchema);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterCompilationStartAction(compilationContext =>
        {
            if (!TryLoadSchema(compilationContext.Options.AdditionalFiles, compilationContext.CancellationToken, out var schemaInfo))
            {
                return;
            }

            compilationContext.RegisterSymbolAction(symbolContext =>
                AnalyzeGenome(symbolContext, schemaInfo), SymbolKind.NamedType);
        });
    }

    private static bool TryLoadSchema(
        ImmutableArray<AdditionalText> additionalFiles,
        CancellationToken cancellationToken,
        out SchemaInfo schema)
    {
        foreach (var file in additionalFiles)
        {
            var fileName = Path.GetFileName(file.Path);
            if (!string.Equals(fileName, "genome.schema.json", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var text = file.GetText(cancellationToken);
            if (text is null)
            {
                break;
            }

            var json = text.ToString();
            var properties = new HashSet<string>(StringComparer.Ordinal);
            string? version = null;

            using var document = JsonDocument.Parse(json, new JsonDocumentOptions
            {
                AllowTrailingCommas = true,
                CommentHandling = JsonCommentHandling.Skip
            });

            var root = document.RootElement;
            if (root.TryGetProperty("version", out var versionElement) &&
                versionElement.ValueKind == JsonValueKind.String)
            {
                version = versionElement.GetString();
            }

            if (root.TryGetProperty("properties", out var propertiesElement) &&
                propertiesElement.ValueKind == JsonValueKind.Object)
            {
                foreach (var property in propertiesElement.EnumerateObject())
                {
                    properties.Add(property.Name);
                }
            }

            if (!string.IsNullOrEmpty(version))
            {
                schema = new SchemaInfo(version!, properties.ToImmutableHashSet(StringComparer.Ordinal));
                return true;
            }

            break;
        }

        schema = default;
        return false;
    }

    private static void AnalyzeGenome(SymbolAnalysisContext context, SchemaInfo schema)
    {
        if (context.Symbol is not INamedTypeSymbol type)
        {
            return;
        }

        if (!string.Equals(type.Name, "Genome", StringComparison.Ordinal) || type.TypeKind != TypeKind.Class)
        {
            return;
        }

        var genomeLocation = type.Locations.FirstOrDefault(static l => l.IsInSource);
        if (genomeLocation is null)
        {
            return;
        }

        var schemaConst = type.GetMembers()
            .OfType<IFieldSymbol>()
            .FirstOrDefault(f => f.IsConst &&
                                 f.DeclaredAccessibility == Accessibility.Public &&
                                 string.Equals(f.Name, "SCHEMA_VERSION", StringComparison.Ordinal));

        if (schemaConst is null || schemaConst.Type.SpecialType != SpecialType.System_String)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                Diagnostics.GenomeSchemaVersionMismatch,
                genomeLocation,
                schema.Version));
        }
        else if (schemaConst.ConstantValue is not string constValue ||
                 !string.Equals(constValue, schema.Version, StringComparison.Ordinal))
        {
            context.ReportDiagnostic(Diagnostic.Create(
                Diagnostics.GenomeSchemaVersionMismatch,
                genomeLocation,
                schema.Version));
        }

        foreach (var member in type.GetMembers())
        {
            if (!SymbolEqualityComparer.Default.Equals(member.ContainingType, type))
            {
                continue;
            }

            if (member.IsImplicitlyDeclared)
            {
                continue;
            }

            switch (member)
            {
                case IFieldSymbol field when field.DeclaredAccessibility == Accessibility.Public:
                    if (field.IsConst && string.Equals(field.Name, "SCHEMA_VERSION", StringComparison.Ordinal))
                    {
                        continue;
                    }

                    if (!schema.Properties.Contains(field.Name))
                    {
                        ReportMemberDiagnostic(context, field, field.Name);
                    }

                    break;
                case IPropertySymbol property when property.DeclaredAccessibility == Accessibility.Public:
                    if (!schema.Properties.Contains(property.Name))
                    {
                        ReportMemberDiagnostic(context, property, property.Name);
                    }

                    break;
            }
        }
    }

    private static void ReportMemberDiagnostic(SymbolAnalysisContext context, ISymbol symbol, string memberName)
    {
        var location = symbol.Locations.FirstOrDefault(static l => l.IsInSource);
        if (location is null)
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(
            Diagnostics.GenomeFieldNotInSchema,
            location,
            memberName));
    }

    private readonly record struct SchemaInfo(string Version, ImmutableHashSet<string> Properties);
}
