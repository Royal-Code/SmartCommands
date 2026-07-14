using System.Diagnostics;

namespace RoyalCode.SmartCommands;

/// <summary>
/// <para>
///     Applied to a static partial class to have the source generator emit a Dependency Injection extension
///     method that registers the services of every generated command handler in the assembly.
/// </para>
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
[Conditional("COMPILE_TIME_ONLY")]
public class AddHandlersServicesAttribute : Attribute
{
    /// <summary>
    /// Initializes a new instance of the <see cref="AddHandlersServicesAttribute"/> class.
    /// </summary>
    /// <param name="title">The name used to build the generated extension method, e.g. <c>Add{title}HandlersServices</c>.</param>
    public AddHandlersServicesAttribute(string title) { }
}
