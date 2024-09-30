using FluentAssertions;
using Microsoft.CodeAnalysis;

namespace Coreum.NewCommands.Tests.Scenarios.Es;

public class Tests
{
    [Theory]
    [InlineData(CreateSomeSyncCode.Command, CreateSomeSyncCode.Interface, CreateSomeSyncCode.Handler)]
    [InlineData(CreateSomeAsyncCode.Command, CreateSomeAsyncCode.Interface, CreateSomeAsyncCode.Handler)]
    [InlineData(CreateSomeSyncWithResultCode.Command, CreateSomeSyncWithResultCode.Interface, CreateSomeSyncWithResultCode.Handler)]
    [InlineData(CreateSomeAsyncWithResultCode.Command, CreateSomeAsyncWithResultCode.Interface, CreateSomeAsyncWithResultCode.Handler)]
    [InlineData(CreateSomeWithDecoratorsSyncCode.Command, CreateSomeWithDecoratorsSyncCode.Interface, CreateSomeWithDecoratorsSyncCode.Handler)]
    [InlineData(CreateSomeWithDecoratorsAsyncCode.Command, CreateSomeWithDecoratorsAsyncCode.Interface, CreateSomeWithDecoratorsAsyncCode.Handler)]
    [InlineData(CreateSomeWithDecoratorsSyncWithResultCode.Command, CreateSomeWithDecoratorsSyncWithResultCode.Interface, CreateSomeWithDecoratorsSyncWithResultCode.Handler)]
    [InlineData(CreateSomeWithDecoratorsAsyncWithResultCode.Command, CreateSomeWithDecoratorsAsyncWithResultCode.Interface, CreateSomeWithDecoratorsAsyncWithResultCode.Handler)]
    public void EsTests(string commandCode, string interfaceCode, string handlerCode)
    {
        Util.Compile(commandCode, out var output, out var diagnostics);

        diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).Should().BeEmpty();

        var generatedInterface = output.SyntaxTrees.Skip(1).FirstOrDefault()?.ToString();
        generatedInterface.Should().Be(interfaceCode);

        var generatedHandler = output.SyntaxTrees.Skip(2).FirstOrDefault()?.ToString();
        generatedHandler.Should().Be(handlerCode);
    }
}
