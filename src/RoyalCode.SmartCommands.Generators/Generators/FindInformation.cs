using Microsoft.CodeAnalysis;

namespace RoyalCode.SmartCommands.Generators.Generators;

public class FindInformation : TransformationGeneratorBase, IEquatable<FindInformation>
{
    private readonly bool canGenerate;

    public FindInformation(Diagnostic diagnostic)
    {
        canGenerate = false;
        AddError(diagnostic);
    }

    public FindInformation(
        TypeDescriptor entityType,
        TypeDescriptor modelType,
        string endpointRoutePattern,
        string endpointName,
        string? description,
        string? groupName)
    {
        canGenerate = true;
        EntityType = entityType;
        ModelType = modelType;
        EndpointRoutePattern = endpointRoutePattern;
        EndpointName = endpointName;
        Description = description;
        GroupName = groupName;
    }

#nullable disable

    public TypeDescriptor EntityType { get; }

    public TypeDescriptor ModelType { get; }
    
    public string EndpointRoutePattern { get; }
    
    public string EndpointName { get; }

#nullable enable

    public string? Description { get; }
    public string? GroupName { get; }


    protected override void Generate(SourceProductionContext spc, bool hasErrors)
    {
        throw new NotImplementedException();
    }

    public bool Equals(FindInformation other)
    {
        throw new NotImplementedException();
    }

    public override bool Equals(object obj)
    {
        return obj is FindInformation fi && Equals(fi);
    }

    public override int GetHashCode()
    {
        return 0; // or implement a specific hash code algorithm
    }
}
