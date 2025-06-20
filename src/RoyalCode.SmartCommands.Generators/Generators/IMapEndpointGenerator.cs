using Microsoft.CodeAnalysis;

namespace RoyalCode.SmartCommands.Generators.Generators;

public interface IMapEndpointGenerator
{
    public string GroupName { get; }

    public void Generate(SourceProductionContext spc, GeneratorNodeList commands, GeneratorNodeList methods);
}