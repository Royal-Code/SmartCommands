
namespace Coreum.NewCommands.Generators.Generators;

public sealed class MapInformation : IEquatable<MapInformation>
{

#nullable disable

    public string HttpMethod { get; set; }

    public string RoutePattern { get; set; }

    public string EndpointName { get; set; }

#nullable enable

    public string? Description { get; set; }

    public string? GroupName { get; set; }

    public MapCreatedInformation? CreatedInformation { get; set; }

    public bool MapIdResultValue { get; set; }

    public MapResponseValuesInformation? ResponseValues { get; set; }

    public bool Equals(MapInformation? other)
    {
        return other is not null &&
            HttpMethod == other.HttpMethod &&
            RoutePattern == other.RoutePattern &&
            EndpointName == other.EndpointName &&
            Description == other.Description &&
            GroupName == other.GroupName &&
            Equals(CreatedInformation, other.CreatedInformation) &&
            MapIdResultValue == other.MapIdResultValue &&
            Equals(ResponseValues, other.ResponseValues);
    }

    public override bool Equals(object? obj)
    {
        return obj is MapInformation description && Equals(description);
    }

    public override int GetHashCode()
    {
        int hashCode = 216910772;
        hashCode = hashCode * -1521134295 + HttpMethod.GetHashCode();
        hashCode = hashCode * -1521134295 + RoutePattern.GetHashCode();
        hashCode = hashCode * -1521134295 + EndpointName.GetHashCode();
        hashCode = hashCode * -1521134295 + Description?.GetHashCode() ?? 0;
        hashCode = hashCode * -1521134295 + GroupName?.GetHashCode() ?? 0;
        hashCode = hashCode * -1521134295 + CreatedInformation?.GetHashCode() ?? 0;
        hashCode = hashCode * -1521134295 + MapIdResultValue.GetHashCode();
        hashCode = hashCode * -1521134295 + ResponseValues?.GetHashCode() ?? 0;
        return hashCode;
    }
}