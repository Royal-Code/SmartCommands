using System.Diagnostics.CodeAnalysis;

namespace RoyalCode.SmartCommands;

[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public class MapFindAttribute<TEntity> : Attribute
    where TEntity : class
{
    public MapFindAttribute([StringSyntax("Route")] string endpointRoutePattern, string endpointName) { }
}
