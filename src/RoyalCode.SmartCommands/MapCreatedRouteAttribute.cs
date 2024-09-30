using System.Diagnostics.CodeAnalysis;

namespace Coreum.NewCommands;

[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public class MapCreatedRouteAttribute : Attribute
{
    public MapCreatedRouteAttribute([StringSyntax("Route")] string endpointRoutePattern, params string[] propertiesNames) { }
}