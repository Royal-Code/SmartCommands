namespace RoyalCode.SmartCommands;

[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public class MapResponseValuesAttribute : Attribute
{
    public MapResponseValuesAttribute(params string[] propertiesNames) { }
}