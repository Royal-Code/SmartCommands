using System.Diagnostics;

namespace RoyalCode.SmartCommands;

[AttributeUsage(AttributeTargets.Class, Inherited = false)]
[Conditional("COMPILE_TIME_ONLY")]
public class AddHandlersServicesAttribute : Attribute
{
    public AddHandlersServicesAttribute(string title) { }
}
