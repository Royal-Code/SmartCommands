using System.Diagnostics.CodeAnalysis;

namespace RoyalCode.SmartCommands;

[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public class MapGetAttribute : Attribute
{
    public MapGetAttribute([StringSyntax("Route")] string endpointRoutePattern, string endpointName) { }
}