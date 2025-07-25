namespace RoyalCode.SmartCommands;

/// <summary>
/// Applies a policy requirement to an endpoint generated from a command.
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public class WithPolicyAttribute : Attribute
{
    /// <summary>
    /// Initializes a new instance of the <see cref="WithPolicyAttribute"/> class with the specified policy.
    /// </summary>
    /// <param name="policy">A string array of policy names to apply to the endpoint.</param>
    public WithPolicyAttribute(params string[] policy) { }
}