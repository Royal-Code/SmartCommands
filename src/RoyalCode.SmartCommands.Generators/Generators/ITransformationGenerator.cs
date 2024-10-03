﻿using Microsoft.CodeAnalysis;

namespace RoyalCode.SmartCommands.Generators.Generators;

public interface ITransformationGenerator
{
    public void Generate(SourceProductionContext spc);
}

public interface ITransformationGenerator<in TModel>
{
    public void Generate(SourceProductionContext spc, IEnumerable<TModel> models);
}