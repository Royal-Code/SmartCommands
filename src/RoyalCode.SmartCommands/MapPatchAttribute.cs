using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace Coreum.NewCommands;

[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public class MapPatchAttribute : Attribute
{
    public MapPatchAttribute([StringSyntax("Route")] string endpointRoutePattern, string endpointName) { }
}
