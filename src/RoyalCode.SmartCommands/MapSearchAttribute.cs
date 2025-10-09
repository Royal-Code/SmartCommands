using System.Diagnostics.CodeAnalysis;

namespace RoyalCode.SmartCommands;

[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public class MapSearchAttribute : Attribute
{
    public MapSearchAttribute([StringSyntax("Route")] string endpointRoutePattern, string endpointName) { }
}

