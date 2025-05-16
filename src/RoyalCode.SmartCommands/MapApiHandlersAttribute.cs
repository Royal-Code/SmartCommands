using System.Diagnostics;

namespace RoyalCode.SmartCommands;

[AttributeUsage(AttributeTargets.Class, Inherited = false)]
[Conditional("COMPILE_TIME_ONLY")]
public class MapApiHandlersAttribute : Attribute { }

[AttributeUsage(AttributeTargets.Class, Inherited = false)]
[Conditional("COMPILE_TIME_ONLY")]
public class MapApiGroupsAttribute : Attribute 
{ 
    public string Name { get; }

    public MapApiGroupsAttribute(string name)
    {
        Name = name;
    }
}