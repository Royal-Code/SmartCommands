using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace Coreum.NewCommands;

[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public class MapPutAttribute : Attribute
{
    public MapPutAttribute([StringSyntax("Route")] string endpointRoutePattern, string endpointName) { }
}
