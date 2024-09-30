
using Coreum.NewCommands.Generators.Models.Descriptors;

namespace Coreum.NewCommands.Generators.Generators;

public sealed class AddHandlersServicesInformation : IEquatable<AddHandlersServicesInformation>
{
    public AddHandlersServicesInformation(TypeDescriptor classType, string title)
    {
        ClassType = classType;
        Title = title;
    }

    public TypeDescriptor ClassType { get; set; }

    public string Title { get; set; }

    public bool Equals(AddHandlersServicesInformation? other)
    {
        return other is not null &&
            Equals(ClassType, other.ClassType) && 
            Equals(Title, other.Title);
    }

    public override bool Equals(object? obj)
    {
        return obj is AddHandlersServicesInformation descriptor && Equals(descriptor);
    }

    public override int GetHashCode()
    {
        int hashCode = -1279160376;
        hashCode = hashCode * -1521134295 + ClassType.GetHashCode();
        hashCode = hashCode * -1521134295 + Title.GetHashCode();
        return hashCode;
    }
}
