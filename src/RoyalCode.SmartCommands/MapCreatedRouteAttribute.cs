using System.Diagnostics.CodeAnalysis;

namespace RoyalCode.SmartCommands;

/// <summary>
/// <para>
///     Used with <see cref="MapPostAttribute"/> so the generated endpoint returns a <c>201 Created</c> response
///     with a <c>Location</c> header pointing to the created resource, instead of the default response.
/// </para>
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public class MapCreatedRouteAttribute : Attribute
{
    /// <summary>
    /// Initializes a new instance of the <see cref="MapCreatedRouteAttribute"/> class.
    /// </summary>
    /// <param name="endpointRoutePattern">
    ///     The route pattern used to build the <c>Location</c> header, e.g. <c>"/{id}"</c>.
    /// </param>
    /// <param name="propertiesNames">
    ///     The names of the properties, from the value returned by the command, used to fill the route pattern placeholders.
    /// </param>
    public MapCreatedRouteAttribute([StringSyntax("Route")] string endpointRoutePattern, params string[] propertiesNames) { }
}