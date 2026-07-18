namespace RoyalCode.SmartCommands;

/// <summary>
/// <para>
///     Adds an endpoint filter to the generated Minimal API endpoint. The attribute is repeatable and the
///     filters are applied in the declaration order, using the standard
///     <c>AddEndpointFilter&lt;TFilter&gt;()</c> of ASP.NET Core — instances are resolved/activated with the
///     application's dependency injection.
/// </para>
/// <para>
///     <typeparamref name="TFilter"/> must be a non-abstract, non-generic, top-level class implementing
///     <c>Microsoft.AspNetCore.Http.IEndpointFilter</c>. The contract is validated at compile time by the
///     generator (RCCMD051); this package does not reference ASP.NET Core, so the constraint is semantic,
///     not declared on the attribute.
/// </para>
/// <para>
///     Can be used on command classes mapped by the HTTP verbs, on <see cref="MapFindAttribute"/> classes and
///     on <see cref="MapSearchAttribute"/> classes.
/// </para>
/// </summary>
/// <typeparam name="TFilter">The endpoint filter type, implementing <c>IEndpointFilter</c>.</typeparam>
[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = true)]
public class WithEndpointFilterAttribute<TFilter> : Attribute
    where TFilter : class;
