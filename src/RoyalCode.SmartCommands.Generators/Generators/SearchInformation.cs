using Microsoft.CodeAnalysis;
namespace RoyalCode.SmartCommands.Generators.Generators;

internal class SearchInformation : IEquatable<SearchInformation>, IMapEndpointGenerator
{
    private readonly List<Diagnostic>? errors;

    public SearchInformation(Diagnostic diagnostic)
    {
        errors = [diagnostic];
    }

    public SearchInformation(
        TypeDescriptor entityType,
        TypeDescriptor? selectType,
        TypeDescriptor filterType,
        string endpointRoutePattern,
        string endpointName,
        string? description,
        string? summary,
        string[]? authorizationPolicies,
        string groupName)
    {
        EntityType = entityType;
        SelectType = selectType;
        FilterType = filterType;
        EndpointRoutePattern = endpointRoutePattern;
        EndpointName = endpointName;
        Description = description;
        Summary = summary;
        AuthorizationPolicies = authorizationPolicies;
        GroupName = groupName;
    }

    public TypeDescriptor EntityType { get; }

    public TypeDescriptor? SelectType { get; }

    public TypeDescriptor FilterType { get; }

    public string EndpointRoutePattern { get; }

    public string EndpointName { get; }

    public string GroupName { get; }

    public string? Description { get; }

    public string? Summary { get; }

    public string[]? AuthorizationPolicies { get; set; }

    public bool Equals(SearchInformation other)
    {
        if (other is null)
            return false;

        return ReferenceEquals(this, other) ||
            EntityType.Equals(other.EntityType) &&
                SelectType?.Equals(other.SelectType) == true &&
                FilterType.Equals(other.FilterType) &&
                EndpointRoutePattern == other.EndpointRoutePattern &&
                EndpointName == other.EndpointName &&
                GroupName == other.GroupName &&
                Description == other.Description &&
                Summary == other.Summary &&
                AuthorizationPolicies?.SequenceEqual(other.AuthorizationPolicies ?? []) == true;
    }

    public override bool Equals(object obj)
    {
        return obj is SearchInformation other && Equals(other);
    }

    public override int GetHashCode()
    {
        var hashCode = -737078483;
        hashCode = hashCode * -1521134295 + EntityType.GetHashCode();
        hashCode = hashCode * -1521134295 + (SelectType?.GetHashCode() ?? 0);
        hashCode = hashCode * -1521134295 + FilterType.GetHashCode();
        hashCode = hashCode * -1521134295 + EndpointRoutePattern.GetHashCode();
        hashCode = hashCode * -1521134295 + EndpointName.GetHashCode();
        hashCode = hashCode * -1521134295 + GroupName.GetHashCode();
        hashCode = hashCode * -1521134295 + (Description?.GetHashCode() ?? 0);
        hashCode = hashCode * -1521134295 + (Summary?.GetHashCode() ?? 0);
        return hashCode;
    }

    public void Generate(SourceProductionContext spc, GeneratorNodeList commands, GeneratorNodeList methods)
    {
        if (errors is not null && errors.Count > 0)
        {
            errors.ForEach(spc.ReportDiagnostic);
            return;
        }

        throw new NotImplementedException();
    }
}
