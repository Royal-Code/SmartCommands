
using Microsoft.CodeAnalysis;
using RoyalCode.SmartCommands.Generators.Models.Descriptors;

namespace RoyalCode.SmartCommands.Generators.Generators;

public sealed class AddHandlersServicesInformation : TransformationGeneratorBase<ServiceTypeDescriptor>, IEquatable<AddHandlersServicesInformation>
{
    public AddHandlersServicesInformation(TypeDescriptor classType, string title, List<Diagnostic> errors)
    {
        ClassType = classType;
        Title = title;

        if (errors is not null && errors.Count > 0)
            Errors = errors;
    }

    public TypeDescriptor ClassType { get; set; }

    public string Title { get; set; }

    public bool Equals(AddHandlersServicesInformation? other)
    {
        return other is not null &&
            Equals(ClassType, other.ClassType) &&
            Equals(Title, other.Title) &&
            EqualErrors(other);
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
        hashCode = hashCode * -1521134295 + Errors?.GetHashCode() ?? 0;
        return hashCode;
    }

    protected override void Generate(SourceProductionContext spc, IEnumerable<ServiceTypeDescriptor> models, bool hasErrors)
    {
        AddHandlersServicesGenerator.Generate(spc, this, models);
    }
}
