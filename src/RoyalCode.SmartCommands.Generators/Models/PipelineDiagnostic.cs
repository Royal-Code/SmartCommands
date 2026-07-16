using System.Globalization;
using Microsoft.CodeAnalysis;
using RoyalCode.Extensions.SourceGenerator.Collections;
using RoyalCode.Extensions.SourceGenerator.Diagnostics;

namespace RoyalCode.SmartCommands.Generators.Models;

internal static class PipelineDiagnostic
{
    internal static EquatableArray<DiagnosticInfo> Snapshot(IEnumerable<Diagnostic> diagnostics) =>
        new(diagnostics.Select(Snapshot));

    internal static DiagnosticInfo Snapshot(Diagnostic diagnostic) =>
        DiagnosticInfo.Create(
            diagnostic.Descriptor,
            diagnostic.Location,
            diagnostic.GetMessage(CultureInfo.InvariantCulture));

    internal static void Report(SourceProductionContext context, EquatableArray<DiagnosticInfo> diagnostics)
    {
        foreach (var diagnostic in diagnostics)
        {
            context.CancellationToken.ThrowIfCancellationRequested();
            context.ReportDiagnostic(diagnostic.ToDiagnostic(CmdDiagnostics.ResolveRendered));
        }
    }
}
