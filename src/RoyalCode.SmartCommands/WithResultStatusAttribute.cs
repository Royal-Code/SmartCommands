namespace RoyalCode.SmartCommands;

/// <summary>
/// <para>
///     Selects, explicitly, the success status produced by a mapped command endpoint
///     (<see cref="MapPostAttribute"/> and the other HTTP verbs). Without this attribute the generator keeps
///     the current inference — see <see cref="HttpResultStatus"/>.
/// </para>
/// <para>
///     Rules validated at compile time: <see cref="MapCreatedRouteAttribute"/> implies
///     <see cref="HttpResultStatus.Created"/> and cannot be combined with an explicit <c>Ok</c> or
///     <c>NoContent</c>; <c>NoContent</c> cannot be combined with <see cref="MapIdResultValueAttribute"/> or
///     <see cref="MapResponseValuesAttribute"/> (they declare a response body); the attribute applies only to
///     command maps, not to <see cref="MapFindAttribute"/> or <see cref="MapSearchAttribute"/> classes.
/// </para>
/// <para>Example:</para>
/// <para>
/// <code>
/// // responds 204, deliberately discarding the success value of Result&lt;Produto&gt;
/// [MapGroup("produtos")]
/// [MapPost("/{id}/arquivar", "arquivar-produto")]
/// [WithResultStatus(HttpResultStatus.NoContent)]
/// public class ArquivarProduto { /* ... */ }
/// </code>
/// </para>
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public class WithResultStatusAttribute : Attribute
{
    /// <summary>
    /// Initializes a new instance of the <see cref="WithResultStatusAttribute"/> class.
    /// </summary>
    /// <param name="status">The explicit success status produced by the endpoint.</param>
    public WithResultStatusAttribute(HttpResultStatus status) { }
}
