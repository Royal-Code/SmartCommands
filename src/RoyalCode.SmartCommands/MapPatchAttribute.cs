using System.Diagnostics.CodeAnalysis;

namespace RoyalCode.SmartCommands;

[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public class MapPatchAttribute : Attribute
{
    public MapPatchAttribute([StringSyntax("Route")] string endpointRoutePattern, string endpointName) { }
}
