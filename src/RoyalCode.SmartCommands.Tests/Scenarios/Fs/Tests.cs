using FluentAssertions;
using Microsoft.CodeAnalysis;

namespace RoyalCode.SmartCommands.Tests.Scenarios.Fs;

public class Tests
{
    [Theory]
    [InlineData(EditSomeSyncCode.Command, EditSomeSyncCode.Interface, EditSomeSyncCode.Handler)]
    public void FsTests(string commandCode, string interfaceCode, string handlerCode)
    {
        Util.Compile(commandCode, out var output, out var diagnostics);

        diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).Should().BeEmpty();

        var generatedInterface = output.SyntaxTrees.Skip(1).FirstOrDefault()?.ToString();
        generatedInterface.Should().Be(interfaceCode);

        var generatedHandler = output.SyntaxTrees.Skip(2).FirstOrDefault()?.ToString();
        generatedHandler.Should().Be(handlerCode);
    }
}
