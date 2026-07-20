using System.Diagnostics.CodeAnalysis;

namespace RoyalCode.SmartCommands;

/// <summary>
/// <para>
///     Used with <see cref="MapPostAttribute"/> so the generated endpoint returns a <c>201 Created</c> response
///     with a <c>Location</c> header pointing to the created resource, instead of the default response.
/// </para>
/// <para>
///     The route pattern uses named placeholders, like <c>"{id}"</c>, matched case-insensitively to the
///     properties of the value returned by the command, declared in <c>propertiesNames</c>
///     (prefer <c>nameof</c>). Each placeholder must match exactly one declared property, and every declared
///     property must be used by a placeholder; mismatches, duplications and unknown or unreadable properties
///     are reported at compile time (RCCMD050). At runtime, each property value is formatted with the
///     invariant culture and URI-escaped as one path segment before it is inserted into the <c>Location</c>.
/// </para>
/// <para>Example:</para>
/// <para>
/// <code>
/// [MapGroup("api/products")]
/// [MapPost("/", "create-product")]
/// [MapCreatedRoute("{id}", nameof(Product.Id))]
/// public class CreateProduct
/// {
///     // the generated handler responds 201 with Location "api/products/{created.Id}"
/// }
/// </code>
/// </para>
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public class MapCreatedRouteAttribute : Attribute
{
    /// <summary>
    /// Initializes a new instance of the <see cref="MapCreatedRouteAttribute"/> class.
    /// </summary>
    /// <param name="endpointRoutePattern">
    ///     The route pattern used to build the <c>Location</c> header, with named placeholders,
    ///     e.g. <c>"/{id}"</c>. When the command declares <see cref="MapGroupAttribute"/>, the group prefix
    ///     is prepended to the generated location.
    /// </param>
    /// <param name="propertiesNames">
    ///     The names of the properties, from the value returned by the command, matched (case-insensitively)
    ///     to the named placeholders of the route pattern. Prefer declaring them with <c>nameof</c>.
    /// </param>
    public MapCreatedRouteAttribute([StringSyntax("Route")] string endpointRoutePattern, params string[] propertiesNames) { }
}
