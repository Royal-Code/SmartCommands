using Microsoft.CodeAnalysis;
using Xunit;

namespace RoyalCode.SmartCommands.Tests.Scenarios.Is;

public class Tests
{
    [Theory]
    [InlineData(CreateSomeMapIdCode.Command, CreateSomeMapIdCode.Map)]
    [InlineData(CreateSomeMapIdCodeWithAuthorization.Command, CreateSomeMapIdCodeWithAuthorization.Map)]
    [InlineData(CreateSomeMapIdCodeWithPolicy0.Command, CreateSomeMapIdCodeWithPolicy0.Map)]
    [InlineData(CreateSomeMapIdCodeWithPolicy1.Command, CreateSomeMapIdCodeWithPolicy1.Map)]
    [InlineData(CreateSomeMapIdCodeWithPolicy2.Command, CreateSomeMapIdCodeWithPolicy2.Map)]
    [InlineData(CreateSomeMapIdCodeWithPolicy3.Command, CreateSomeMapIdCodeWithPolicy3.Map)]
    public void IsTestsWithId(string commandCode, string mapCode)
    {
        Util.Compile(commandCode, out var output, out var diagnostics);

        Assert.Empty(diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));

        var generatedMap = output.SyntaxTrees.Skip(3).FirstOrDefault()?.ToString();
        Assert.Equal(mapCode, generatedMap);
    }

    [Theory]
    [InlineData(CreateSomeWithMapResponseValuesCode.Command, CreateSomeWithMapResponseValuesCode.ResponseModel, CreateSomeWithMapResponseValuesCode.Map)]
    public void IsTestsWithResponseValues(string commandCode, string responseModel, string mapCode)
    {
        Util.Compile(commandCode, out var output, out var diagnostics);

        Assert.Empty(diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));

        var generatedResponseModel = output.SyntaxTrees.Skip(3).FirstOrDefault()?.ToString();
        Assert.Equal(responseModel, generatedResponseModel);

        var generatedMap = output.SyntaxTrees.Skip(4).FirstOrDefault()?.ToString();
        Assert.Equal(mapCode, generatedMap);
    }
}