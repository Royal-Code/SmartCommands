using System.Diagnostics;

namespace Coreum.NewCommands;

[AttributeUsage(AttributeTargets.Method, Inherited = false)]
[Conditional("COMPILE_TIME_ONLY")]
public class WithDecoratorsAttribute : Attribute { }