using FluentAssertions;
using Microsoft.CodeAnalysis;

namespace RoyalCode.SmartCommands.Tests.Scenarios.Is;

public class Tests
{
    [Theory]
    [InlineData(CreateSomeMapIdCode.Command, CreateSomeMapIdCode.Map)]
    public void IsTestsWithId(string commandCode, string mapCode)
    {
        Util.Compile(commandCode, out var output, out var diagnostics);

        diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).Should().BeEmpty();

        var generatedMap = output.SyntaxTrees.Skip(3).FirstOrDefault()?.ToString();
        generatedMap.Should().Be(mapCode);
    }

    [Theory]
    [InlineData(CreateSomeWithMapResponseValuesCode.Command, CreateSomeWithMapResponseValuesCode.ResponseModel, CreateSomeWithMapResponseValuesCode.Map)]
    public void IsTestsWithResponseValues(string commandCode, string responseModel, string mapCode)
    {
        Util.Compile(commandCode, out var output, out var diagnostics);

        diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).Should().BeEmpty();

        var generatedResponseModel = output.SyntaxTrees.Skip(3).FirstOrDefault()?.ToString();
        generatedResponseModel.Should().Be(responseModel);

        var generatedMap = output.SyntaxTrees.Skip(4).FirstOrDefault()?.ToString();
        generatedMap.Should().Be(mapCode);
    }
}