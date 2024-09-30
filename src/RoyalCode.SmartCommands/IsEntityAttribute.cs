using System.Diagnostics;

namespace Coreum.NewCommands;

[AttributeUsage(AttributeTargets.Parameter | AttributeTargets.Property, Inherited = false)]
[Conditional("COMPILE_TIME_ONLY")]
public class IsEntityAttribute : Attribute { }
