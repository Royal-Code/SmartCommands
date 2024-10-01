using FluentAssertions;
using Microsoft.CodeAnalysis;

namespace RoyalCode.SmartCommands.Tests.Scenarios.Gs;

public class Tests
{
    [Theory]
    [InlineData(DoWithParametersCode.Command, DoWithParametersCode.Interface, DoWithParametersCode.Handler)]
    [InlineData(DoWithTwoParametersCode.Command, DoWithTwoParametersCode.Interface, DoWithTwoParametersCode.Handler)]
    public void GsTests(string commandCode, string interfaceCode, string handlerCode)
    {
        Util.Compile(commandCode, out var output, out var diagnostics);

        diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).Should().BeEmpty();

        var generatedInterface = output.SyntaxTrees.Skip(1).FirstOrDefault()?.ToString();
        generatedInterface.Should().Be(interfaceCode);

        var generatedHandler = output.SyntaxTrees.Skip(2).FirstOrDefault()?.ToString();
        generatedHandler.Should().Be(handlerCode);
    }
}