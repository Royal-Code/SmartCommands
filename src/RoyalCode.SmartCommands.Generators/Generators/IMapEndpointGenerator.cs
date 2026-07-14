using Microsoft.CodeAnalysis;

namespace RoyalCode.SmartCommands.Generators.Generators;

internal interface IMapEndpointGenerator
{
    public string GroupName { get; }

    public void Generate(SourceProductionContext spc, GeneratorNodeList commands, GeneratorNodeList methods, bool withOpenApi);
}