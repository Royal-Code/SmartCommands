using System;
using System.Linq;
using Microsoft.CodeAnalysis;
using Xunit;

namespace RoyalCode.SmartCommands.Tests.Diagnostics;

/// <summary>
/// Diagnósticos de agregação de endpoints: nomes de endpoint (WithName) devem ser únicos entre todos os
/// endpoints mapeados; nomes repetidos produzem RCCMD030 em cada ocorrência (na localização do argumento do
/// atributo) e os endpoints conflitantes são excluídos da emissão do host.
/// </summary>
public class AggregationDiagnosticTests
{
    private const string Usings =
        """
        using RoyalCode.SmartCommands;
        using RoyalCode.SmartProblems;
        using Microsoft.AspNetCore.Routing;

        namespace Tests.Aggregation;

        """;

    [Fact]
    public void Duplicate_endpoint_name_is_reported_per_occurrence_with_location_and_blocks_emission()
    {
        const string code = Usings +
            """
            [MapPost("/a", "same-name")]
            public class CreateA
            {
                [Command]
                public Result Execute() => Result.Ok();
            }

            [MapPost("/b", "same-name")]
            public class CreateB
            {
                [Command]
                public Result Execute() => Result.Ok();
            }

            [MapPost("/c", "unique-name")]
            public class CreateC
            {
                [Command]
                public Result Execute() => Result.Ok();
            }

            [MapApiHandlers]
            public static partial class Endpoints { }
            """;

        Util.Compile(code, out var output, out var diagnostics);

        // uma ocorrência por endpoint conflitante, cada uma na localização do argumento do endpoint name
        var duplicates = diagnostics.Where(d => d.Id == "RCCMD030").ToArray();
        Assert.Equal(2, duplicates.Length);
        foreach (var duplicate in duplicates)
        {
            Assert.Equal(DiagnosticSeverity.Error, duplicate.Severity);
            Assert.NotEqual(Location.None, duplicate.Location);
            Assert.True(duplicate.Location.GetLineSpan().IsValid);
            Assert.Contains("same-name", duplicate.GetMessage(), StringComparison.Ordinal);
        }

        // os endpoints conflitantes não são emitidos; o endpoint válido permanece
        var generatedTrees = output.SyntaxTrees.Skip(1).Select(tree => tree.ToString()).ToArray();
        Assert.DoesNotContain(generatedTrees, source => source.Contains("\"same-name\""));
        Assert.Contains(generatedTrees, source => source.Contains("\"unique-name\""));

        Assert.DoesNotContain(diagnostics, d => d.Id == "CS8785");
    }

    [Fact]
    public void Distinct_endpoint_names_do_not_trigger()
    {
        const string code = Usings +
            """
            [MapPost("/a", "create-a")]
            public class CreateA
            {
                [Command]
                public Result Execute() => Result.Ok();
            }

            [MapPost("/b", "create-b")]
            public class CreateB
            {
                [Command]
                public Result Execute() => Result.Ok();
            }

            [MapApiHandlers]
            public static partial class Endpoints { }
            """;

        Util.Compile(code, out _, out var diagnostics);

        Assert.DoesNotContain(diagnostics, d => d.Id == "RCCMD030");
        Assert.DoesNotContain(diagnostics, d => d.Severity == DiagnosticSeverity.Error);
    }

    [Fact]
    public void Multiple_map_hosts_are_reported_per_occurrence_with_location()
    {
        const string code = Usings +
            """
            [MapPost("/a", "create-a")]
            public class CreateA
            {
                [Command]
                public Result Execute() => Result.Ok();
            }

            [MapApiHandlers]
            public static partial class EndpointsOne { }

            [MapApiHandlers]
            public static partial class EndpointsTwo { }
            """;

        Util.Compile(code, out var output, out var diagnostics);

        var hosts = diagnostics.Where(d => d.Id == "RCCMD012").ToArray();
        Assert.Equal(2, hosts.Length);
        foreach (var host in hosts)
        {
            Assert.Equal(DiagnosticSeverity.Warning, host.Severity);
            Assert.NotEqual(Location.None, host.Location);
            Assert.True(host.Location.GetLineSpan().IsValid);
            var sourceText = output.SyntaxTrees.First().GetText();
            Assert.Equal("MapApiHandlers", sourceText.ToString(host.Location.SourceSpan));
        }

        Assert.DoesNotContain(diagnostics, d => d.Id == "CS8785");
    }
}
