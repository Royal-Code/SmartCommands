using Microsoft.CodeAnalysis;
using Xunit;

namespace RoyalCode.SmartCommands.Tests.Scenarios.Gs;

public class Tests
{
    [Theory]
    [InlineData(DoWithParametersCode.Command, DoWithParametersCode.Interface, DoWithParametersCode.Handler)]
    [InlineData(DoWithTwoParametersCode.Command, DoWithTwoParametersCode.Interface, DoWithTwoParametersCode.Handler)]
    public void GsTests(string commandCode, string interfaceCode, string handlerCode)
    {
        Util.Compile(commandCode, out var output, out var diagnostics);

        Assert.Empty(diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));

        var generatedInterface = output.SyntaxTrees.Skip(1).FirstOrDefault()?.ToString();
        Assert.Equal(interfaceCode, generatedInterface);

        var generatedHandler = output.SyntaxTrees.Skip(2).FirstOrDefault()?.ToString();
        Assert.Equal(handlerCode, generatedHandler);
    }
}