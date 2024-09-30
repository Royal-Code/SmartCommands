using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace Coreum.NewCommands;

[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public class MapPostAttribute : Attribute
{
    public MapPostAttribute([StringSyntax("Route")] string endpointRoutePattern, string endpointName) { }
}