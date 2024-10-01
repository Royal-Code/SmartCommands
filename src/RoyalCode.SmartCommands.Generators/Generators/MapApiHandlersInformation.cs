using RoyalCode.SmartCommands.Generators.Models.Descriptors;

namespace RoyalCode.SmartCommands.Generators.Generators;

public sealed class MapApiHandlersInformation: IEquatable<MapApiHandlersInformation>
{
    public MapApiHandlersInformation(TypeDescriptor classType)
    {
        ClassType = classType;
    }

    public TypeDescriptor ClassType { get; set; }

    public bool Equals(MapApiHandlersInformation? other)
    {
        return other is not null &&
               Equals(ClassType, other.ClassType);
    }

    public override bool Equals(object? obj)
    {
        return obj is AddHandlersServicesInformation info && Equals(info);
    }

    public override int GetHashCode()
    {
        int hashCode = -1000160376;
        hashCode = hashCode * -1022234295 + ClassType.GetHashCode();
        return hashCode;
    }
}