using System.Diagnostics.CodeAnalysis;

namespace RoyalCode.SmartCommands;

/// <summary>
/// <para>
///     Maps an endpoint to handle HTTP PATCH requests.
/// </para>
/// <para>
///     Use this attribute in classes that define a command for partially updating resources.
/// </para>
/// <para>
///     Typically used with <see cref="EditEntityAttribute{TEntity, TId}"/> to edit an existing entity.
/// </para>
/// <para>
///     Combine with <see cref="MapGroupAttribute"/> to group related endpoints, and with <see cref="WithDescriptionAttribute"/>, <see cref="WithSummaryAttribute"/>, <see cref="WithAuthorizationAttribute"/>, or <see cref="WithPolicyAttribute"/> for additional metadata and security.
/// </para>
/// <para>
///     Example:
///     <code>
///     [MapGroup("api/products")]
///     [MapPatch("/{id}", "patch-product")]
///     public class PatchProduct { ... }
///     </code>
/// </para>
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public class MapPatchAttribute : Attribute
{
    /// <summary>
    /// Initializes a new instance of the <see cref="MapPatchAttribute"/> class.
    /// </summary>
    /// <param name="endpointRoutePattern">
    ///     The route pattern for the endpoint, e.g. <c>"/{id}"</c>.
    /// </param>
    /// <param name="endpointName">
    ///     The name of the endpoint, e.g. <c>"patch-product"</c>.
    /// </param>
    public MapPatchAttribute([StringSyntax("Route")] string endpointRoutePattern, string endpointName) { }
}
