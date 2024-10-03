using Microsoft.CodeAnalysis;
using RoyalCode.SmartCommands.Generators.Models.Descriptors;

namespace RoyalCode.SmartCommands.Generators.Generators;

public sealed class MapApiHandlersInformation: TransformationGeneratorBase<CommandHandlerInformation>, IEquatable<MapApiHandlersInformation>
{
    public MapApiHandlersInformation(TypeDescriptor classType, List<Diagnostic>? diagnostics)
    {
        ClassType = classType;

        if (diagnostics is not null && diagnostics.Count > 0)
            Errors = diagnostics;
    }

    public TypeDescriptor ClassType { get; set; }

    public bool Equals(MapApiHandlersInformation? other)
    {
        return other is not null &&
               Equals(ClassType, other.ClassType) &&
               EqualErrors(other);
    }

    public override bool Equals(object? obj)
    {
        return obj is AddHandlersServicesInformation info && Equals(info);
    }

    public override int GetHashCode()
    {
        int hashCode = -1000160376;
        hashCode = hashCode * -1022234295 + ClassType.GetHashCode();
        hashCode = hashCode * -1022234295 + Errors?.GetHashCode() ?? 0;
        return hashCode;
    }

    protected override void Generate(SourceProductionContext spc, IEnumerable<CommandHandlerInformation> models, bool hasErrors)
    {
        MapApiHandlersGenerator.Generate(spc, this, models);
    }
}