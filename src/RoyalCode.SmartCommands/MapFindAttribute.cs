using System.Diagnostics.CodeAnalysis;

namespace RoyalCode.SmartCommands;

/// <summary>
/// <para>
///     Maps an enpoint to find a specific entity.
/// </para>
/// <para>
///     This attribute should be used in classes with a DTO (Data Transfer Object) function,
///     and use the attribute <see cref="EntityReferenceAttribute{TEntity, TId}"/> to reference
///     the entity and its ID type.
/// </para>
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public class MapFindAttribute : Attribute
{
    /// <summary>
    /// Maps an endpoint to find a specific entity.
    /// </summary>
    /// <param name="endpointRoutePattern">
    ///     The route pattern for the endpoint, e.g. <c>"{id}"</c>.
    /// </param>
    /// <param name="endpointName">
    ///     The name of the endpoint, e.g. <c>"find-entity-by-id"</c>.
    /// </param>
    public MapFindAttribute([StringSyntax("Route")] string endpointRoutePattern, string endpointName) { }
}
