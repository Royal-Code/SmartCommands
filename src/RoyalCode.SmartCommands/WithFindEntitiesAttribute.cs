using System.Diagnostics;

namespace RoyalCode.SmartCommands;

[AttributeUsage(AttributeTargets.Method, Inherited = false)]
[Conditional("COMPILE_TIME_ONLY")]
public class WithFindEntitiesAttribute<T> : Attribute
    where T : class
{ }