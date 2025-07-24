using System.Diagnostics.CodeAnalysis;

namespace RoyalCode.SmartCommands;

[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public class MapFindAttribute : Attribute
{
    public MapFindAttribute([StringSyntax("Route")] string endpointRoutePattern, string endpointName) { }
}

[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public class EntityReferenceAttribute<TEntity, TId> : Attribute
    where TEntity : class
{ }
