using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.CodeAnalysis;
using RoyalCode.Extensions.SourceGenerator.Diagnostics;
using RoyalCode.SmartCommands.Generators;
using RoyalCode.SmartCommands.Generators.Models;
using Xunit;

namespace RoyalCode.SmartCommands.Tests.Diagnostics;

/// <summary>
/// Guards the single RCCMD catalog (DF15): every declared descriptor must be registered and resolvable, must be
/// documented in <c>AnalyzerReleases</c>, and no id may reach the output without a <see cref="DiagnosticDescriptor"/>.
/// </summary>
public class DiagnosticCatalogTests
{
    private static readonly DiagnosticDescriptor[] Descriptors = typeof(CmdDiagnostics)
        .GetFields(BindingFlags.Public | BindingFlags.Static)
        .Where(field => field.FieldType == typeof(DiagnosticDescriptor))
        .Select(field => (DiagnosticDescriptor)field.GetValue(null)!)
        .ToArray();

    [Fact]
    public void Every_declared_descriptor_is_registered_and_resolvable()
    {
        Assert.NotEmpty(Descriptors);
        foreach (var descriptor in Descriptors)
        {
            Assert.Contains(descriptor.Id, CmdDiagnostics.CatalogIds);
            Assert.Same(descriptor, CmdDiagnostics.Get(descriptor.Id));
        }
    }

    [Fact]
    public void Catalog_ids_are_unique()
    {
        var ids = Descriptors.Select(descriptor => descriptor.Id).ToArray();
        Assert.Equal(ids.Length, ids.Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public void Every_catalog_id_is_documented_in_analyzer_releases()
    {
        var documented = ReadAnalyzerReleaseIds();
        foreach (var id in CmdDiagnostics.CatalogIds)
            Assert.Contains(id, documented);
    }

    [Fact]
    public void Get_rejects_unknown_ids()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => CmdDiagnostics.Get("RCCMD999"));
    }

    [Fact]
    public void Pipeline_preserves_real_message_arguments_before_reconstructing_the_diagnostic()
    {
        var diagnostic = DiagnosticInfo.Create(
            CmdDiagnostics.ReservedIdentifier,
            Location.None,
            "handler");

        var snapshot = PipelineDiagnostic.Snapshot([diagnostic]);

        var retained = Assert.Single(snapshot);
        Assert.Equal("RCCMD029", retained.Id);
        Assert.Equal("handler", Assert.Single(retained.Arguments));
        Assert.Contains(
            "handler",
            retained.ToDiagnostic(CmdDiagnostics.Get).GetMessage(),
            StringComparison.Ordinal);
    }

    private static HashSet<string> ReadAnalyzerReleaseIds()
    {
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var fileName in new[] { "AnalyzerReleases.Shipped.md", "AnalyzerReleases.Unshipped.md" })
        {
            foreach (var line in File.ReadAllLines(FindUpwards(fileName)))
            {
                var trimmed = line.TrimStart();
                if (trimmed.StartsWith("RCCMD", StringComparison.Ordinal))
                    ids.Add(trimmed.Split('|', ' ')[0].Trim());
            }
        }
        return ids;
    }

    private static string FindUpwards(string fileName)
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            var candidate = Path.Combine(dir.FullName, fileName);
            if (File.Exists(candidate))
                return candidate;
        }

        throw new FileNotFoundException($"Could not locate {fileName} above {AppContext.BaseDirectory}.");
    }
}
