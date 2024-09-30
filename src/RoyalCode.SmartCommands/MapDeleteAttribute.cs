using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace Coreum.NewCommands;

[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public class MapDeleteAttribute : Attribute
{
    public MapDeleteAttribute([StringSyntax("Route")] string endpointRoutePattern, string endpointName) { }
}
