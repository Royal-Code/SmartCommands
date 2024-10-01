using System.Diagnostics.CodeAnalysis;

namespace RoyalCode.SmartCommands;

[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public class MapPostAttribute : Attribute
{
    public MapPostAttribute([StringSyntax("Route")] string endpointRoutePattern, string endpointName) { }
}