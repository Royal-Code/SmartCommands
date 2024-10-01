using System.Diagnostics;

namespace RoyalCode.SmartCommands;

[AttributeUsage(AttributeTargets.Parameter | AttributeTargets.Property, Inherited = false)]
[Conditional("COMPILE_TIME_ONLY")]
public class IsEntityAttribute : Attribute { }
