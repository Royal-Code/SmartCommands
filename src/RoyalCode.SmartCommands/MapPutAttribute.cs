using System.Diagnostics.CodeAnalysis;

namespace RoyalCode.SmartCommands;

/// <summary>
/// <para>
///     Maps a command class to an HTTP <c>PUT</c> endpoint in Minimal API.
/// </para>
/// <para>
///     Use this attribute on a class that contains a method marked with <see cref="CommandAttribute"/>.
///     It is intended for full updates or replacement of an existing resource.
/// </para>
/// <para>
///     Commonly combined with <see cref="EditEntityAttribute{TEntity, TId}"/> to load the entity being updated.
///     The handler will usually return <c>200 OK</c> when the update is successful.
/// </para>
/// <para>
///     Combine with <see cref="MapGroupAttribute"/> to group endpoints; with <see cref="WithDescriptionAttribute"/>
///     and <see cref="WithSummaryAttribute"/> for metadata; and with <see cref="WithAuthorizationAttribute"/>
///     or <see cref="WithPolicyAttribute"/> to enforce authorization or policies.
/// </para>
/// <para>
///     Endpoint handler code is generated via <see cref="MapApiHandlersAttribute"/> in a static partial class.
/// </para>
/// <para>Example:</para>
/// <para>
/// <code>
/// [MapGroup("api/products")]
/// [MapPut("/{id}", "update-product")]
/// public class UpdateProduct
/// {
///     public string Name { get; set; }
///     public bool Active { get; set; }
///
///     [Command, EditEntity{Product, int}, WithUnitOfWork{AppDbContext}]
///     internal Result Execute(Product entity)
///     {
///         entity.Name = Name;
///         entity.Active = Active;
///         return Result.Ok();
///     }
/// }
/// </code>
/// </para>
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public class MapPutAttribute : Attribute
{
    /// <summary>
    /// Initializes a new instance of the <see cref="MapPutAttribute"/> class.
    /// </summary>
    /// <param name="endpointRoutePattern">
    ///     The route pattern for the endpoint, typically <c>"/{id}"</c> for updating a specific resource within a group.
    /// </param>
    /// <param name="endpointName">
    ///     A unique name for the endpoint, e.g. <c>"update-product"</c>.
    /// </param>
    public MapPutAttribute([StringSyntax("Route")] string endpointRoutePattern, string endpointName) { }
}
