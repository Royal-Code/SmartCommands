using System.Diagnostics;

namespace RoyalCode.SmartCommands;

[AttributeUsage(AttributeTargets.Parameter, Inherited = false)]
[Conditional("COMPILE_TIME_ONLY")]
public class WithParameterAttribute : Attribute { }