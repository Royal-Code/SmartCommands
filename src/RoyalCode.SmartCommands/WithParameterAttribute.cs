using System.Diagnostics;

namespace Coreum.NewCommands;

[AttributeUsage(AttributeTargets.Parameter, Inherited = false)]
[Conditional("COMPILE_TIME_ONLY")]
public class WithParameterAttribute : Attribute { }