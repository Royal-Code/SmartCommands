using System.Diagnostics.CodeAnalysis;

namespace RoyalCode.SmartCommands;

[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public class MapDeleteAttribute : Attribute
{
    public MapDeleteAttribute([StringSyntax("Route")] string endpointRoutePattern, string endpointName) { }
}
