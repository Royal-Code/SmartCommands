using System.Diagnostics.CodeAnalysis;

namespace Coreum.NewCommands;

[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public class MapGroupAttribute : Attribute
{
    public MapGroupAttribute([StringSyntax("Route")] string endpointRoutePattern) { }
}