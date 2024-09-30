using System.Diagnostics;

namespace Coreum.NewCommands;

[AttributeUsage(AttributeTargets.Class, Inherited = false)]
[Conditional("COMPILE_TIME_ONLY")]
public class AddHandlersServicesAttribute : Attribute
{
    public AddHandlersServicesAttribute(string title) { }
}
