namespace RoyalCode.SmartCommands;

/// <summary>
/// <para>
///     The explicit success status that a mapped command endpoint produces, used with
///     <see cref="WithResultStatusAttribute"/>.
/// </para>
/// <para>
///     Without an explicit selection, the generator keeps the current inference: commands respond
///     <c>200 OK</c> with the value of the <c>Result</c>, <see cref="MapCreatedRouteAttribute"/> produces
///     <c>201 Created</c>, and <c>MapDelete</c> without a returned value produces <c>204 No Content</c>.
/// </para>
/// </summary>
public enum HttpResultStatus
{
    /// <summary>
    ///     <c>200 OK</c>: the response body is the success value of the command result, including the
    ///     projections of <see cref="MapIdResultValueAttribute"/> or <see cref="MapResponseValuesAttribute"/>.
    /// </summary>
    Ok = 200,

    /// <summary>
    ///     <c>201 Created</c>: the resource was created. The <c>Location</c> header is produced only when the
    ///     command also declares <see cref="MapCreatedRouteAttribute"/>; without it, the response is a
    ///     <c>201</c> without <c>Location</c>.
    /// </summary>
    Created = 201,

    /// <summary>
    ///     <c>204 No Content</c>: the success value of a <c>Result&lt;T&gt;</c> is deliberately discarded and
    ///     the response has no body. Problems are always preserved and produce their regular status codes.
    /// </summary>
    NoContent = 204,
}
