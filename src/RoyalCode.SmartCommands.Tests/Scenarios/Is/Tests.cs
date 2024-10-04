using FluentAssertions;
using Microsoft.CodeAnalysis;

namespace RoyalCode.SmartCommands.Tests.Scenarios.Is;

public class Tests
{
    [Theory]
    [InlineData(MapIdCode.Command, MapIdCode.Map)]
    public void GsTests(string commandCode, string mapCode)
    {
        Util.Compile(commandCode, out var output, out var diagnostics);

        diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).Should().BeEmpty();

        var generatedMap = output.SyntaxTrees.Skip(3).FirstOrDefault()?.ToString();
        generatedMap.Should().Be(mapCode);
    }
}