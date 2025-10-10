using System;
using System.Collections.Immutable;
using System.IO;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace WormWorld.Analyzers;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class RandomOutsideRngServiceAnalyzer : DiagnosticAnalyzer
{
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(Diagnostics.RandomOutsideRngService);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterCompilationStartAction(startContext =>
        {
            var systemRandom = startContext.Compilation.GetTypeByMetadataName("System.Random");
            var unityRandom = startContext.Compilation.GetTypeByMetadataName("UnityEngine.Random");

            if (systemRandom is null && unityRandom is null)
            {
                return;
            }

            startContext.RegisterOperationAction(
                ctx => AnalyzeObjectCreation(ctx, systemRandom, unityRandom),
                OperationKind.ObjectCreation);

            startContext.RegisterOperationAction(
                ctx => AnalyzeInvocation(ctx, systemRandom, unityRandom),
                OperationKind.Invocation);

            startContext.RegisterOperationAction(
                ctx => AnalyzeMemberReference(ctx, systemRandom, unityRandom),
                OperationKind.MemberReference);
        });
    }

    private static void AnalyzeObjectCreation(
        OperationAnalysisContext context,
        INamedTypeSymbol? systemRandom,
        INamedTypeSymbol? unityRandom)
    {
        if (IsInAllowedContext(context))
        {
            return;
        }

        if (context.Operation is IObjectCreationOperation creation &&
            IsRandomType(creation.Type, systemRandom, unityRandom))
        {
            Report(context, creation.Syntax.GetLocation());
        }
    }

    private static void AnalyzeInvocation(
        OperationAnalysisContext context,
        INamedTypeSymbol? systemRandom,
        INamedTypeSymbol? unityRandom)
    {
        if (IsInAllowedContext(context))
        {
            return;
        }

        if (context.Operation is IInvocationOperation invocation)
        {
            var containingType = invocation.TargetMethod?.ContainingType;
            if (IsRandomType(containingType, systemRandom, unityRandom))
            {
                Report(context, invocation.Syntax.GetLocation());
            }
        }
    }

    private static void AnalyzeMemberReference(
        OperationAnalysisContext context,
        INamedTypeSymbol? systemRandom,
        INamedTypeSymbol? unityRandom)
    {
        if (IsInAllowedContext(context))
        {
            return;
        }

        if (context.Operation is IMemberReferenceOperation memberReference)
        {
            if (IsRandomType(memberReference.Member?.ContainingType, systemRandom, unityRandom))
            {
                Report(context, memberReference.Syntax.GetLocation());
            }
        }
    }

    private static bool IsRandomType(
        ITypeSymbol? type,
        INamedTypeSymbol? systemRandom,
        INamedTypeSymbol? unityRandom)
    {
        if (type is null)
        {
            return false;
        }

        if (systemRandom is not null && SymbolEqualityComparer.Default.Equals(type, systemRandom))
        {
            return true;
        }

        if (unityRandom is not null && SymbolEqualityComparer.Default.Equals(type, unityRandom))
        {
            return true;
        }

        if (type is INamedTypeSymbol named && named.ConstructedFrom is not null)
        {
            return IsRandomType(named.ConstructedFrom, systemRandom, unityRandom);
        }

        return false;
    }

    private static bool IsInAllowedContext(OperationAnalysisContext context)
    {
        var containingType = context.ContainingSymbol?.ContainingType;
        if (containingType is not null && string.Equals(containingType.Name, "RngService", StringComparison.Ordinal))
        {
            return true;
        }

        var syntaxTree = context.Operation.Syntax.SyntaxTree;
        var filePath = syntaxTree?.FilePath;
        if (!string.IsNullOrEmpty(filePath))
        {
            var fileName = Path.GetFileName(filePath);
            if (string.Equals(fileName, "RngService.cs", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static void Report(OperationAnalysisContext context, Location location)
    {
        context.ReportDiagnostic(Diagnostic.Create(Diagnostics.RandomOutsideRngService, location));
    }
}
