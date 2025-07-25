using System.Diagnostics.CodeAnalysis;

namespace RoyalCode.SmartCommands;

/// <summary>
/// <para>
///     Groups related endpoints under a common route prefix.
/// </para>
/// <para>
///     Use this attribute on a class to define a group for endpoints, typically for organizing related commands.
/// </para>
/// <para>
///     Combine with <see cref="MapPostAttribute"/>, <see cref="MapPutAttribute"/>, <see cref="MapPatchAttribute"/>, <see cref="MapDeleteAttribute"/>, or <see cref="MapGetAttribute"/> to map endpoints within the group.
/// </para>
/// <para>
///     Example:
///     <code>
///     [MapGroup("api/products")]
///     [MapPost("/", "create-product")]
///     public class CreateProduct { ... }
///     </code>
/// </para>
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public class MapGroupAttribute : Attribute
{
    /// <summary>
    /// Initializes a new instance of the <see cref="MapGroupAttribute"/> class.
    /// </summary>
    /// <param name="endpointRoutePattern">
    ///     The route prefix for the group, e.g. <c>"api/products"</c>.
    /// </param>
    public MapGroupAttribute([StringSyntax("Route")] string endpointRoutePattern) { }
}