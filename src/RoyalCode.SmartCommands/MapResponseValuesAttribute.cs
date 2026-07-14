namespace RoyalCode.SmartCommands;

/// <summary>
/// <para>
///     Indicates that the generated endpoint should return a response body composed of a subset of the
///     properties of the value produced by the command, instead of the full value.
/// </para>
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public class MapResponseValuesAttribute : Attribute
{
    /// <summary>
    /// Initializes a new instance of the <see cref="MapResponseValuesAttribute"/> class.
    /// </summary>
    /// <param name="propertiesNames">
    ///     The names of the properties, from the value returned by the command, to include in the response body.
    /// </param>
    public MapResponseValuesAttribute(params string[] propertiesNames) { }
}