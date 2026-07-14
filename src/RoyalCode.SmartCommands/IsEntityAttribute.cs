using System.Diagnostics;

namespace RoyalCode.SmartCommands;

/// <summary>
/// <para>
///     Explicitly marks a command method parameter, or a property of the command class, as representing an entity,
///     overriding the automatic entity detection performed by the source generator.
/// </para>
/// </summary>
[AttributeUsage(AttributeTargets.Parameter | AttributeTargets.Property, Inherited = false)]
[Conditional("COMPILE_TIME_ONLY")]
public class IsEntityAttribute : Attribute { }
