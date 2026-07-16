using Microsoft.CodeAnalysis;
using RoyalCode.Extensions.SourceGenerator.Collections;
using RoyalCode.Extensions.SourceGenerator.Diagnostics;

namespace RoyalCode.SmartCommands.Generators.Models;

internal static class PipelineDiagnostic
{
    internal static EquatableArray<DiagnosticInfo> Snapshot(IEnumerable<DiagnosticInfo> diagnostics) =>
        new(diagnostics);

    internal static void Report(SourceProductionContext context, EquatableArray<DiagnosticInfo> diagnostics)
    {
        foreach (var diagnostic in diagnostics)
        {
            context.CancellationToken.ThrowIfCancellationRequested();
            context.ReportDiagnostic(diagnostic.ToDiagnostic(CmdDiagnostics.Get));
        }
    }
}
