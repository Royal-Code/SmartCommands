using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace Coreum.NewCommands;

[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public class MapGetAttribute : Attribute
{
    public MapGetAttribute([StringSyntax("Route")] string endpointRoutePattern, string endpointName) { }
}