namespace RoyalCode.SmartCommands;

/// <summary>
/// Specifies a summary for an endpoint generated from a command.
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public class WithSummaryAttribute : Attribute
{
    /// <summary>
    /// Initializes a new instance of the <see cref="WithSummaryAttribute"/> class with the specified summary.
    /// </summary>
    /// <param name="summary">The summary of the endpoint.</param>
    public WithSummaryAttribute(string summary) { }
}
