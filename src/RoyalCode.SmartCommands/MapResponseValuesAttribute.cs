namespace Coreum.NewCommands;

[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public class MapResponseValuesAttribute : Attribute
{
    public MapResponseValuesAttribute(params string[] propertiesNames) { }
}