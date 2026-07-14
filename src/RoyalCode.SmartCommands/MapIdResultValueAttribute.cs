namespace RoyalCode.SmartCommands;

/// <summary>
/// <para>
///     Indicates that the generated endpoint should return only the <c>Id</c> property of the value produced by
///     the command, instead of the full value.
/// </para>
/// <para>
///     The type returned by the command method must expose an <c>Id</c> property; otherwise a compile-time
///     diagnostic is reported.
/// </para>
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public class MapIdResultValueAttribute : Attribute { }