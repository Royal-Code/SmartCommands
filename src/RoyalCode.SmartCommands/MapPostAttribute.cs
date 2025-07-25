using System.Diagnostics.CodeAnalysis;

namespace RoyalCode.SmartCommands;

/// <summary>
/// <para>
///     Maps an endpoint to handle HTTP POST requests.
/// </para>
/// <para>
///     This attribute should be used in classes that define a command,
///     and it is typically used to create or update resources.
/// </para>
/// <para>
///     To define a command, use the <see cref="CommandAttribute"/> in a method,
///     and then use this attribute on the class to map the endpoint.
/// </para>
/// <para>
///     Use algo the <see cref="MapGroupAttribute"/> to group related endpoints,
///     and the <see cref="WithDescriptionAttribute"/> to provide a description of the endpoint,
///     and the <see cref="WithSummaryAttribute"/> to provide a summary of the endpoint.
///     Use the <see cref="WithAuthorizationAttribute"/> to require authorization for the endpoint,
///     or the <see cref="WithPolicyAttribute"/> to require a specific policy for the endpoint.
/// </para>
/// <para>
///     To manipulate the response object, can be used the <see cref="MapResponseValuesAttribute"/>
///     or the <see cref="MapIdResultValueAttribute"/>.
/// </para>
/// <para>
///     For creation of a new resource, the endpoint should return a 201 Created status code,
///     then the <see cref="MapCreatedRouteAttribute"/> can be used to specify the route
///     of the newly created resource (location header).
/// </para>
/// <para>
///     To generate the endpoint code, use the <see cref="MapApiHandlersAttribute"/> in a static partial class.
/// </para>
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public class MapPostAttribute : Attribute
{
    /// <summary>
    /// Initializes a new instance of the <see cref="MapPostAttribute"/> class.
    /// </summary>
    /// <param name="endpointRoutePattern">
    ///     The route pattern for the endpoint, 
    ///     e.g. <c>""</c> for the root endpoint that creates a new entity/resource,
    ///     or <c>"{id}/something"</c> for an endpoint that handles a specific resource.
    /// </param>
    /// <param name="endpointName">
    ///     The name of the endpoint, e.g. <c>"create-entity"</c>.
    /// </param>
    public MapPostAttribute([StringSyntax("Route")] string endpointRoutePattern, string endpointName) { }
}