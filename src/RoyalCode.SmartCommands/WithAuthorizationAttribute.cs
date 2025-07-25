namespace RoyalCode.SmartCommands;

/// <summary>
/// Applies authorization requirements to an endpoint generated from a command.
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public class WithAuthorizationAttribute : Attribute { }
