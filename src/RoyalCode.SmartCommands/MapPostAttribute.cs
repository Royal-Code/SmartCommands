using System.Diagnostics.CodeAnalysis;

namespace RoyalCode.SmartCommands;

/// <summary>
/// <para>
///     Maps a command class to an HTTP <c>POST</c> endpoint in Minimal API.
/// </para>
/// <para>
///     Use this attribute on a class that contains a method marked with <see cref="CommandAttribute"/>.
///     It is typically used to create a new resource or to execute an action that does not map to a specific existing resource.
/// </para>
/// <para>
///     Combine with <see cref="MapGroupAttribute"/> to group related endpoints; with
///     <see cref="WithDescriptionAttribute"/> and <see cref="WithSummaryAttribute"/> to provide metadata; and with
///     <see cref="WithAuthorizationAttribute"/> or <see cref="WithPolicyAttribute"/> to require authorization or policies.
/// </para>
/// <para>
///     To manipulate the response payload you can use <see cref="MapResponseValuesAttribute"/> to project specific properties
///     or <see cref="MapIdResultValueAttribute"/> to expose the <c>Id</c> property of the returned object.
/// </para>
/// <para>
///     When the command creates a new entity (using <see cref="ProduceNewEntityAttribute"/>) you may combine
///     <see cref="MapCreatedRouteAttribute"/> so the generated handler returns <c>201 Created</c> and sets the <c>Location</c> header.
///     When editing an existing entity (using <see cref="EditEntityAttribute{TEntity, TId}"/>) the handler will usually return <c>200 OK</c>.
/// </para>
/// <para>
///     To generate the endpoint handler code use <see cref="MapApiHandlersAttribute"/> in a static partial class.
/// </para>
/// <para>Examples:</para>
/// <para>
/// <code>
/// // Basic command without entity creation
/// [MapGroup("api/products")]
/// [MapPost("/", "create-product")]
/// public class CreateProduct
/// {
///     public string Name { get; set; }
///
///     [Command]
///     internal Result Execute() => Result.Ok();
/// }
///
/// // Creating a new entity and returning 201 Created with Location
/// [MapGroup("api/products")]
/// [MapPost("/", "create-product")]
/// [MapCreatedRoute("{0}", "Id")]
/// public class CreateProductWithEntity
/// {
///     public string Name { get; set; }
///
///     [Command, ProduceNewEntity, WithUnitOfWork{AppDbContext}]
///     internal Product Execute(AppDbContext db)
///         => new Product { Name = Name, Active = true };
/// }
///
/// // Creating from an existing entity (EditEntity + MapCreatedRoute)
/// [MapGroup("api/products")]
/// [MapPost("/{sourceId}", "duplicate-product")]
/// [MapCreatedRoute("{0}", "Id")]
/// public class DuplicateProduct
/// {
///     public string sourceId { get; set; }
///
///     [Command, EditEntity{Product, int}, WithUnitOfWork{AppDbContext}]
///     internal Product Execute(Product source, AppDbContext db)
///         => new Product { Name = source.Name, Active = source.Active };
/// }
/// </code>
/// </para>
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public class MapPostAttribute : Attribute
{
    /// <summary>
    /// Initializes a new instance of the <see cref="MapPostAttribute"/> class.
    /// </summary>
    /// <param name="endpointRoutePattern">
    ///     The route pattern for the endpoint. For creation usually <c>"/"</c> (root of the group),
    ///     or a pattern like <c>"{id}/action"</c> for commands that act relative to an existing resource.
    /// </param>
    /// <param name="endpointName">
    ///     A unique name for the endpoint (used by Minimal API metadata), e.g. <c>"create-product"</c>.
    /// </param>
    public MapPostAttribute([StringSyntax("Route")] string endpointRoutePattern, string endpointName) { }
}