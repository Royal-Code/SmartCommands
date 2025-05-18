using System.Diagnostics.CodeAnalysis;

namespace RoyalCode.SmartCommands;

[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public class MapSearchAttribute<TEntity> : Attribute
    where TEntity : class
{
    public MapSearchAttribute([StringSyntax("Route")] string endpointRoutePattern, string endpointName) { }
}

[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public class MapSearchAttribute<TEntity, TModel> : Attribute
    where TEntity : class
    where TModel : class
{
    public MapSearchAttribute([StringSyntax("Route")] string endpointRoutePattern, string endpointName) { }
}