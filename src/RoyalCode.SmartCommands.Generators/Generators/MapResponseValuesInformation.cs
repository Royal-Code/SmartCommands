namespace RoyalCode.SmartCommands.Generators.Generators;

internal sealed class MapResponseValuesInformation : IEquatable<MapResponseValuesInformation>
{
    public IList<PropertyDescriptor> PropertiesNames { get; set; }

    public MapResponseValuesInformation(IList<PropertyDescriptor> propertiesNames)
    {
        PropertiesNames = propertiesNames;
    }

    public bool Equals(MapResponseValuesInformation? other)
    {
        if (other is null)
            return false;
        return ReferenceEquals(this, other) ||
               PropertiesNames.Equals(other.PropertiesNames);
    }

    public override bool Equals(object? obj)
    {
        return obj is MapResponseValuesInformation other && Equals(other);
    }

    public override int GetHashCode()
    {
        return PropertiesNames.GetHashCode();
    }
}