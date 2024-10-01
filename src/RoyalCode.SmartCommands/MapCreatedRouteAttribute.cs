using System.Diagnostics.CodeAnalysis;

namespace RoyalCode.SmartCommands;

[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public class MapCreatedRouteAttribute : Attribute
{
    public MapCreatedRouteAttribute([StringSyntax("Route")] string endpointRoutePattern, params string[] propertiesNames) { }
}