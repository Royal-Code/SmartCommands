using System.Diagnostics;

namespace RoyalCode.SmartCommands;

/// <summary>
/// <para>
///     Marks a command method parameter as coming from outside the command payload: instead of being read from
///     the command instance, the generated handler exposes it as an additional Minimal API endpoint parameter,
///     bound the usual ASP.NET Core way (route, query, services, etc.).
/// </para>
/// <para>
///     Cannot be combined with a parameter that is already recognized as an entity, a collection of entities,
///     or the unit of work context.
/// </para>
/// </summary>
[AttributeUsage(AttributeTargets.Parameter, Inherited = false)]
[Conditional("COMPILE_TIME_ONLY")]
public class WithParameterAttribute : Attribute { }