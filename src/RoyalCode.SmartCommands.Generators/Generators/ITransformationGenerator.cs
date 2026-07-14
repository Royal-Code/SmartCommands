using Microsoft.CodeAnalysis;

namespace RoyalCode.SmartCommands.Generators.Generators;

internal interface ITransformationGenerator
{
    public void Generate(SourceProductionContext spc);
}

internal interface ITransformationGenerator<in TModel>
{
    public void Generate(SourceProductionContext spc, IEnumerable<TModel> models);
}
