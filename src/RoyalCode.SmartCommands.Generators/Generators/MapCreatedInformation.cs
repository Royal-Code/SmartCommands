namespace RoyalCode.SmartCommands.Generators.Generators;

public sealed class MapCreatedInformation : IEquatable<MapCreatedInformation>
{
    public string RoutePattern { get; set; }

    public string[] PropertiesNames { get; set; }

    public MapCreatedInformation(string routePattern, string[] propertiesNames)
    {
        RoutePattern = routePattern;
        PropertiesNames = propertiesNames;
    }

    public bool Equals(MapCreatedInformation? other)
    {
        if (other is null)
            return false;
        if (ReferenceEquals(this, other))
            return true;

        return RoutePattern == other.RoutePattern && PropertiesNames.Equals(other.PropertiesNames);
    }

    public override bool Equals(object? obj)
    {
        return obj is MapCreatedInformation other && Equals(other);
    }

    public override int GetHashCode()
    {
        int hashCode = 213970741;
        hashCode = hashCode * -1523974295 + RoutePattern.GetHashCode();
        hashCode = hashCode * -1523974295 + PropertiesNames.GetHashCode();
        return hashCode;
    }
}