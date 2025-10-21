using System.Diagnostics.CodeAnalysis;

namespace RoyalCode.SmartCommands;

/// <summary>
/// <para>
///     Maps a command class to an HTTP <c>GET</c> endpoint in Minimal API for retrieving data (single resource or computed result).
/// </para>
/// <para>
///     Use this attribute on a class with a method marked by <see cref="CommandAttribute"/> that does not modify state.
///     For finding a single entity by id prefer <see cref="MapFindAttribute"/>, while <see cref="MapGetAttribute"/> can be used
///     for derived data, summaries, or resource representations that do not strictly follow the find pattern.
/// </para>
/// <para>
///     Combine with <see cref="MapGroupAttribute"/> to group related query endpoints; add metadata using
///     <see cref="WithDescriptionAttribute"/> and <see cref="WithSummaryAttribute"/>; and apply authorization with
///     <see cref="WithAuthorizationAttribute"/> or <see cref="WithPolicyAttribute"/> when needed.
/// </para>
/// <para>
///     Endpoint handler code is generated using <see cref="MapApiHandlersAttribute"/> in a static partial class.
/// </para>
/// <para>Example:</para>
/// <para>
/// <code>
/// // Get product summary (computed data)
/// [MapGroup("api/products")]
/// [MapGet("/summary", "get-products-summary")]
/// public class GetProductsSummary
/// {
///     [Command, WithUnitOfWork{AppDbContext}]
///     internal Result Execute(AppDbContext db)
///     {
///         var total = db.Set{Product}().Count();
///         var active = db.Set{Product}().Count(p => p.Active);
///         return Result.Ok(new { Total = total, Active = active });
///     }
/// }
/// </code>
/// </para>
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public class MapGetAttribute : Attribute
{
    /// <summary>
    /// Initializes a new instance of the <see cref="MapGetAttribute"/> class.
    /// </summary>
    /// <param name="endpointRoutePattern">
    ///     The route pattern for the endpoint, e.g. <c>"/summary"</c> or <c>"/{id}"</c>.
    /// </param>
    /// <param name="endpointName">
    ///     A unique name for the endpoint, e.g. <c>"get-products-summary"</c>.
    /// </param>
    public MapGetAttribute([StringSyntax("Route")] string endpointRoutePattern, string endpointName) { }
}