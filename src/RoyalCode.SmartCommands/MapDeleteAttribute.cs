using System.Diagnostics.CodeAnalysis;

namespace RoyalCode.SmartCommands;

/// <summary>
/// <para>
///     Maps a command class to an HTTP <c>DELETE</c> endpoint in Minimal API.
/// </para>
/// <para>
///     Use this attribute on a class that contains a method marked with <see cref="CommandAttribute"/> that removes or deactivates
///     an existing resource. For soft delete or deactivate scenarios combine with <see cref="EditEntityAttribute{TEntity, TId}"/>.
/// </para>
/// <para>
///     Group related endpoints with <see cref="MapGroupAttribute"/>; add metadata using <see cref="WithDescriptionAttribute"/>
///     and <see cref="WithSummaryAttribute"/>; and enforce authorization through <see cref="WithAuthorizationAttribute"/>
///     or <see cref="WithPolicyAttribute"/>.
/// </para>
/// <para>
///     Endpoint handler code is generated via <see cref="MapApiHandlersAttribute"/>.
/// </para>
/// <para>Examples:</para>
/// <para>
/// <code>
/// // Hard delete by id
/// [MapGroup("api/products")]
/// [MapDelete("/{id}", "delete-product")]
/// public class DeleteProduct
/// {
///     [Command, EditEntity{Product, int}, WithUnitOfWork{AppDbContext}]
///     internal Result Execute(Product entity, AppDbContext db)
///     {
///         db.Remove(entity);
///         return Result.Ok();
///     }
/// }
///
/// // Soft delete (deactivate)
/// [MapGroup("api/products")]
/// [MapDelete("/{id}", "deactivate-product")]
/// public class DeactivateProduct
/// {
///     [Command, EditEntity{Product, int}, WithUnitOfWork{AppDbContext}]
///     internal Result Execute(Product entity)
///     {
///         entity.Active = false;
///         return Result.Ok();
///     }
/// }
/// </code>
/// </para>
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public class MapDeleteAttribute : Attribute
{
    /// <summary>
    /// Initializes a new instance of the <see cref="MapDeleteAttribute"/> class.
    /// </summary>
    /// <param name="endpointRoutePattern">
    ///     The route pattern for the endpoint, typically <c>"/{id}"</c> when deleting a specific resource.
    /// </param>
    /// <param name="endpointName">
    ///     A unique name for the endpoint, e.g. <c>"delete-product"</c>.
    /// </param>
    public MapDeleteAttribute([StringSyntax("Route")] string endpointRoutePattern, string endpointName) { }
}
