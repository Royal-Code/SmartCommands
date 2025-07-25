namespace RoyalCode.SmartCommands;

/// <summary>
/// Specifies a description for an endpoint generated from a command.
/// </summary>
/// <remarks>
/// Apply this attribute to a class to provide a human-readable description.
/// </remarks>
/// <example>
/// <code>
/// [WithDescription("This command does something useful.")]
/// public class MyCommand { }
/// </code>
/// </example>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public class WithDescriptionAttribute : Attribute
{
    /// <summary>
    /// Initializes a new instance of the <see cref="WithDescriptionAttribute"/> class with the specified description.
    /// </summary>
    /// <param name="description">The description of the endpoint.</param>
    public WithDescriptionAttribute(string description) { }
}