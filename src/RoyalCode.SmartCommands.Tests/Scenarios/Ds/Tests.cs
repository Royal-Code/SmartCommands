using Microsoft.CodeAnalysis;
using Xunit;

namespace RoyalCode.SmartCommands.Tests.Scenarios.Ds;

public class Tests
{
    [Theory]
    [InlineData(DoSomethingSyncCode.Command, DoSomethingSyncCode.Interface, DoSomethingSyncCode.Handler)]
    [InlineData(DoSomethingSyncWithResultCode.Command, DoSomethingSyncWithResultCode.Interface, DoSomethingSyncWithResultCode.Handler)]
    [InlineData(DoSomethingSyncWithResultSomeCode.Command, DoSomethingSyncWithResultSomeCode.Interface, DoSomethingSyncWithResultSomeCode.Handler)]
    [InlineData(DoSomethingAsyncCode.Command, DoSomethingAsyncCode.Interface, DoSomethingAsyncCode.Handler)]
    [InlineData(DoSomethingAsyncWithResultCode.Command, DoSomethingAsyncWithResultCode.Interface, DoSomethingAsyncWithResultCode.Handler)]
    [InlineData(DoSomethingAsyncWithResultSomeCode.Command, DoSomethingAsyncWithResultSomeCode.Interface, DoSomethingAsyncWithResultSomeCode.Handler)]
    [InlineData(DoSomethingWithDecoratorsSyncCode.Command, DoSomethingWithDecoratorsSyncCode.Interface, DoSomethingWithDecoratorsSyncCode.Handler)]
    [InlineData(DoSomethingWithDecoratorsSyncWithResultCode.Command, DoSomethingWithDecoratorsSyncWithResultCode.Interface, DoSomethingWithDecoratorsSyncWithResultCode.Handler)]
    [InlineData(DoSomethingWithDecoratorsSyncWithResultSomeCode.Command, DoSomethingWithDecoratorsSyncWithResultSomeCode.Interface, DoSomethingWithDecoratorsSyncWithResultSomeCode.Handler)]
    [InlineData(DoSomethingWithDecoratorsAsyncCode.Command, DoSomethingWithDecoratorsAsyncCode.Interface, DoSomethingWithDecoratorsAsyncCode.Handler)]
    [InlineData(DoSomethingWithDecoratorsAsyncWithResultCode.Command, DoSomethingWithDecoratorsAsyncWithResultCode.Interface, DoSomethingWithDecoratorsAsyncWithResultCode.Handler)]
    [InlineData(DoSomethingWithDecoratorsAsyncWithResultSomeCode.Command, DoSomethingWithDecoratorsAsyncWithResultSomeCode.Interface, DoSomethingWithDecoratorsAsyncWithResultSomeCode.Handler)]
    public void DsTests(string commandCode, string interfaceCode, string handlerCode)
    {
        Util.Compile(commandCode, out var output, out var diagnostics);

        Assert.Empty(diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));

        var generatedInterface = output.SyntaxTrees.Skip(1).FirstOrDefault()?.ToString();
        Assert.Equal(Util.GeneratedCode(interfaceCode), generatedInterface);

        var generatedHandler = output.SyntaxTrees.Skip(2).FirstOrDefault()?.ToString();
        Assert.Equal(Util.GeneratedCode(handlerCode), generatedHandler);
    }
}
