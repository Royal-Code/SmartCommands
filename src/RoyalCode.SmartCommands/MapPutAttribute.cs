using System.Diagnostics.CodeAnalysis;

namespace RoyalCode.SmartCommands;

[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public class MapPutAttribute : Attribute
{
    public MapPutAttribute([StringSyntax("Route")] string endpointRoutePattern, string endpointName) { }
}
