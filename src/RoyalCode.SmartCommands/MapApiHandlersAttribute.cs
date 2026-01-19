using System.Diagnostics;

namespace RoyalCode.SmartCommands;

/// <summary>
/// <para>
///     Indicates that the decorated static partial class will have its API endpoints mapped
///     by the source generator.
/// </para>
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
[Conditional("COMPILE_TIME_ONLY")]
public class MapApiHandlersAttribute : Attribute { }
