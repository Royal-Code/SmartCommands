using System.Diagnostics.CodeAnalysis;

namespace RoyalCode.SmartCommands;

/// <summary>
/// <para>
///     Used with a command map (like <see cref="MapPostAttribute"/>) so the generated endpoint returns a
///     <c>202 Accepted</c> response with a <c>Location</c> header pointing to a resource that can be used
///     to monitor the accepted request. Declaring this attribute implies
///     <see cref="HttpResultStatus.Accepted"/>; combine it with
///     <see cref="WithResultStatusAttribute"/> only for the same status.
/// </para>
/// <para>
///     Unlike <c>201 Created</c>, the <c>Location</c> of a <c>202</c> is optional:
///     <see cref="WithResultStatusAttribute"/> with <see cref="HttpResultStatus.Accepted"/> alone responds
///     <c>202</c> without the header. The response body follows the command result: <c>Result</c> responds
///     without a body and <c>Result&lt;T&gt;</c> responds with the success value.
/// </para>
/// <para>
///     The route pattern uses named placeholders, like <c>"{ticket}"</c>, matched case-insensitively to the
///     properties of the value returned by the command, declared in <c>propertiesNames</c>
///     (prefer <c>nameof</c>). Each placeholder must match exactly one declared property, and every declared
///     property must be used by a placeholder; mismatches, duplications and unknown or unreadable properties
///     are reported at compile time (RCCMD054). A command returning a plain <c>Result</c> has no success
///     value, so the pattern must be a static route, without placeholders.
/// </para>
/// <para>Example:</para>
/// <para>
/// <code>
/// [MapGroup("api/envios")]
/// [MapPost("/", "agendar-envio")]
/// [MapAcceptedRoute("status/{protocolo}", nameof(EnvioAgendado.Protocolo))]
/// public class AgendarEnvio
/// {
///     // the generated handler responds 202 with Location "api/envios/status/{value.Protocolo}"
/// }
/// </code>
/// </para>
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public class MapAcceptedRouteAttribute : Attribute
{
    /// <summary>
    /// Initializes a new instance of the <see cref="MapAcceptedRouteAttribute"/> class.
    /// </summary>
    /// <param name="endpointRoutePattern">
    ///     The route pattern used to build the <c>Location</c> header, with named placeholders,
    ///     e.g. <c>"status/{ticket}"</c>, or a static route when the command returns a plain <c>Result</c>.
    ///     When the command declares <see cref="MapGroupAttribute"/>, the group prefix is prepended to the
    ///     generated location.
    /// </param>
    /// <param name="propertiesNames">
    ///     The names of the properties, from the value returned by the command, matched (case-insensitively)
    ///     to the named placeholders of the route pattern. Prefer declaring them with <c>nameof</c>.
    /// </param>
    public MapAcceptedRouteAttribute([StringSyntax("Route")] string endpointRoutePattern, params string[] propertiesNames) { }
}
