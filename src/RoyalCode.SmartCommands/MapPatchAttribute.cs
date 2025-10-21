using System.Diagnostics.CodeAnalysis;

namespace RoyalCode.SmartCommands;

/// <summary>
/// <para>
///     Maps a command class to an HTTP <c>PATCH</c> endpoint in Minimal API.
/// </para>
/// <para>
///     Use this attribute on a class that contains a method marked with <see cref="CommandAttribute"/>.
///     It is intended for partial updates of an existing resource (e.g. updating a subset of properties).
/// </para>
/// <para>
///     Typically combined with <see cref="EditEntityAttribute{TEntity, TId}"/> to load the target entity before applying modifications.
///     The handler will commonly return <c>200 OK</c> when the update succeeds.
/// </para>
/// <para>
///     Combine with <see cref="MapGroupAttribute"/> to group related endpoints; with
///     <see cref="WithDescriptionAttribute"/> and <see cref="WithSummaryAttribute"/> for metadata; and with
///     <see cref="WithAuthorizationAttribute"/> or <see cref="WithPolicyAttribute"/> to enforce authorization or policies.
/// </para>
/// <para>
///     Endpoint code is generated via <see cref="MapApiHandlersAttribute"/> placed in a static partial class.
/// </para>
/// <para>Example:</para>
/// <para>
/// <code>
/// [MapGroup("api/products")]
/// [MapPatch("/{id}", "patch-product")]
/// public class PatchProduct
/// {
///     public string? Name { get; set; }
///     public bool? Active { get; set; }
///
///     [Command, EditEntity{Product, int}, WithUnitOfWork{AppDbContext}]
///     internal Result Execute(Product entity)
///     {
///         if (Name is not null) entity.Name = Name;
///         if (Active.HasValue) entity.Active = Active.Value;
///         return Result.Ok();
///     }
/// }
/// </code>
/// </para>
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public class MapPatchAttribute : Attribute
{
    /// <summary>
    /// Initializes a new instance of the <see cref="MapPatchAttribute"/> class.
    /// </summary>
    /// <param name="endpointRoutePattern">
    ///     The route pattern for the endpoint, e.g. <c>"/{id}"</c> to target a specific resource within a group.
    /// </param>
    /// <param name="endpointName">
    ///     A unique name for the endpoint, e.g. <c>"patch-product"</c>.
    /// </param>
    public MapPatchAttribute([StringSyntax("Route")] string endpointRoutePattern, string endpointName) { }
}
