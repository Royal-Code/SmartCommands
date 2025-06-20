using Microsoft.CodeAnalysis;

namespace RoyalCode.SmartCommands.Generators.Generators;

public class FindInformation : IEquatable<FindInformation>, IMapEndpointGenerator
{
    private List<Diagnostic>? errors;

    public FindInformation(Diagnostic diagnostic)
    {
        errors = [diagnostic];
    }

    public FindInformation(
        TypeDescriptor entityType,
        TypeDescriptor modelType,
        string endpointRoutePattern,
        string endpointName,
        string? description,
        string? groupName)
    {
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

    public string GroupName { get; }

#nullable enable

    public string? Description { get; }
    

    public bool Equals(FindInformation other)
    {
        if (other is null) 
            return false;

        return ReferenceEquals(this, other) || 
               EntityType.Equals(other.EntityType) && 
               ModelType.Equals(other.ModelType) && 
               EndpointRoutePattern == other.EndpointRoutePattern &&
               EndpointName == other.EndpointName &&
               Description == other.Description &&
               GroupName == other.GroupName &&
               EqualErrors(other);
    }

    private bool EqualErrors(FindInformation other)
    {
        if (errors is null)
            return other.errors is null;

        if (other.errors is null)
            return false;

        return errors.SequenceEqual(other.errors);
    }

    public override bool Equals(object obj)
    {
        return obj is FindInformation fi && Equals(fi);
    }

    public override int GetHashCode()
    {
        int hashCode = -737078483;
        hashCode = hashCode * -1521134295 + EntityType.GetHashCode();
        hashCode = hashCode * -1521134295 + ModelType.GetHashCode();
        hashCode = hashCode * -1521134295 + EndpointRoutePattern.GetHashCode();
        hashCode = hashCode * -1521134295 + EndpointName.GetHashCode();
        hashCode = hashCode * -1521134295 + (Description?.GetHashCode() ?? 0);
        hashCode = hashCode * -1521134295 + (GroupName?.GetHashCode() ?? 0);
        hashCode = hashCode * -1521134295 + (errors?.GetHashCode() ?? 0);
        return hashCode;
    }

    public void Generate(SourceProductionContext spc, GeneratorNodeList commands, GeneratorNodeList methods)
    {
        if (errors is not null && errors.Count > 0)
        {
            errors.ForEach(spc.ReportDiagnostic);
            return;
        }
    }
}
