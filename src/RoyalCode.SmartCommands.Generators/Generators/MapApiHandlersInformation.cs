using Microsoft.CodeAnalysis;
using RoyalCode.Extensions.SourceGenerator.Diagnostics;

namespace RoyalCode.SmartCommands.Generators.Generators;

internal sealed class MapApiHandlersInformation: TransformationGeneratorBase<IMapEndpointGenerator>, IEquatable<MapApiHandlersInformation>
{
    public MapApiHandlersInformation(TypeDescriptor classType, bool withOpenApi, List<DiagnosticInfo>? diagnostics)
    {
        ClassType = classType;
        WithOpenApi = withOpenApi;

        if (diagnostics is not null && diagnostics.Count > 0)
            Errors = diagnostics;
    }

    public TypeDescriptor ClassType { get; set; }

    public bool WithOpenApi { get; set; }

    /// <summary>
    /// Localização do identificador da classe host. Uso exclusivo do transform (vira snapshot no modelo do
    /// pipeline); não participa da igualdade porque a informação é transitória.
    /// </summary>
    public Location? HostLocation { get; set; }

    public bool Equals(MapApiHandlersInformation? other)
    {
        return other is not null &&
               Equals(ClassType, other.ClassType) &&
               WithOpenApi == other.WithOpenApi &&
               EqualErrors(other);
    }

    public override bool Equals(object? obj)
    {
        return obj is MapApiHandlersInformation info && Equals(info);
    }

    public override int GetHashCode()
    {
        int hashCode = -1000160376;
        hashCode = hashCode * -1022234295 + ClassType.GetHashCode();
        hashCode = hashCode * -1022234295 + WithOpenApi.GetHashCode();
        hashCode = hashCode * -1022234295 + Errors?.GetHashCode() ?? 0;
        return hashCode;
    }

    protected override void Generate(SourceProductionContext spc, IEnumerable<IMapEndpointGenerator> models, bool hasErrors)
    {
        MapApiHandlersGenerator.Generate(spc, this, models);
    }
}
