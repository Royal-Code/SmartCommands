using System.Diagnostics.CodeAnalysis;

namespace RoyalCode.SmartCommands;

/// <summary>
/// <para>
///     Maps an endpoint to handle HTTP PUT requests.
/// </para>
/// <para>
///     Use this attribute in classes that define a command for updating resources.
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
///     [MapPut("/{id}", "update-product")]
///     public class UpdateProduct { ... }
///     </code>
/// </para>
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public class MapPutAttribute : Attribute
{
    public MapPutAttribute([StringSyntax("Route")] string endpointRoutePattern, string endpointName) { }
}
