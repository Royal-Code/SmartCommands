using System.Diagnostics.CodeAnalysis;

namespace RoyalCode.SmartCommands;

[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public class MapGroupAttribute : Attribute
{
    public MapGroupAttribute([StringSyntax("Route")] string endpointRoutePattern) { }
}