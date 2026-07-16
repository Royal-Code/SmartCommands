using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using RoyalCode.Extensions.SourceGenerator.Collections;
using RoyalCode.Extensions.SourceGenerator.Descriptors.Snapshots;
using RoyalCode.SmartCommands.Generators.Generators;
using static RoyalCode.SmartCommands.Generators.Generators.CommandHandlerInformation;

namespace RoyalCode.SmartCommands.Generators.Models;

/// <summary>
/// Localização symbol-free e value-equatable, segura para retenção no pipeline incremental. Usada para
/// reportar diagnósticos de agregação (ex.: RCCMD030) no ponto exato do código do usuário sem reter
/// <see cref="Location"/> (que referencia a SyntaxTree).
/// </summary>
internal sealed record LocationModel(string? FilePath, TextSpan SourceSpan, LinePositionSpan LineSpan, bool HasLocation)
{
    internal static readonly LocationModel None = new(null, default, default, false);

    internal static LocationModel Create(Location? location)
    {
        if (location is null || location == Location.None || !location.IsInSource)
            return None;

        var lineSpan = location.GetLineSpan();
        return new LocationModel(lineSpan.Path, location.SourceSpan, lineSpan.Span, true);
    }

    internal Location ToLocation() =>
        HasLocation ? Location.Create(FilePath ?? string.Empty, SourceSpan, LineSpan) : Location.None;
}

internal sealed record ReturnModel(
    TypeSnapshot DeclaredType,
    TypeSnapshot EffectiveType,
    bool IsAwaitable,
    bool IsResult,
    bool IsVoid,
    TypeSnapshot? ValueType)
{
    internal static ReturnModel Create(TypeSnapshot declaredType)
    {
        var effectiveType = declaredType;
        var isAwaitable = IsKnownType(
            effectiveType,
            "System.Threading.Tasks.Task",
            "System.Threading.Tasks.Task`1",
            "System.Threading.Tasks.ValueTask",
            "System.Threading.Tasks.ValueTask`1");

        if (isAwaitable && effectiveType.TypeArguments.Count == 1)
            effectiveType = effectiveType.TypeArguments[0];

        var isResult = IsKnownType(
            effectiveType,
            "RoyalCode.SmartProblems.Result",
            "RoyalCode.SmartProblems.Result`1");
        var isVoid = effectiveType.IsVoid ||
                     IsKnownType(effectiveType, "System.Threading.Tasks.Task", "System.Threading.Tasks.ValueTask");

        TypeSnapshot? valueType = null;
        if (isResult)
        {
            if (effectiveType.TypeArguments.Count == 1)
                valueType = effectiveType.TypeArguments[0];
        }
        else if (!isVoid)
        {
            valueType = effectiveType;
        }

        return new ReturnModel(declaredType, effectiveType, isAwaitable, isResult, isVoid, valueType);
    }

    private static bool IsKnownType(TypeSnapshot type, params string[] metadataNames)
    {
        var identity = PipelineModelConversions.MetadataIdentity(type);
        return identity is not null && metadataNames.Contains(identity, StringComparer.Ordinal);
    }
}

/// <summary>
/// Um atributo de binding do ASP.NET Core capturado do parâmetro-fonte (DF3): o nome curto do atributo
/// (ex.: <c>FromQuery</c>) e o argumento opcional <c>Name</c>. Copiado somente para o delegate Minimal API.
/// </summary>
internal sealed record ParameterBindingModel(string Attribute, string? Name);

internal sealed record ParameterModel(
    ParameterSnapshot Snapshot,
    bool IsCancellationToken,
    bool IsEntity,
    bool IsContext,
    bool IsHandlerParameter,
    bool IsCollectionOfEntities,
    EquatableArray<ParameterBindingModel> Bindings)
{
    internal static ParameterModel Create(
        ParameterDescriptor descriptor,
        EquatableArray<ParameterBindingModel> bindings = default)
    {
        var snapshot = ParameterSnapshot.CreateFromHints(descriptor);
        return new ParameterModel(
            snapshot,
            PipelineModelConversions.MetadataIdentity(snapshot.TypeUsage.Type) == "System.Threading.CancellationToken",
            snapshot.TypeUsage.IsEntity,
            snapshot.TypeUsage.IsContext,
            snapshot.TypeUsage.IsHandlerParameter,
            snapshot.TypeUsage.IsCollectionOfEntities,
            bindings);
    }
}

internal sealed record CommandCoreModel(
    TypeSnapshot ModelType,
    bool HasWithValidateModel,
    bool HasWithDecorators,
    string MethodName,
    bool MethodIsAsync,
    bool HandlerMustBeAsync,
    TypeSnapshot MethodReturnType,
    TypeSnapshot HandlerReturnType,
    ReturnModel Return,
    EquatableArray<ParameterModel> Parameters,
    string HandlerInterfaceName,
    string HandlerImplementationName,
    EquatableArray<string> NotNullProperties,
    bool HasWithUnitOfWork,
    bool HasWithFindEntities,
    TypeSnapshot? ContextAccessorType,
    ContextAccessorModes ContextAccessorMode,
    EquatableArray<IdPropertyBindingSnapshot> IdPropertiesBindings,
    EquatableArray<string> ProduceProblems,
    TypeSnapshot? ProduceNewEntityType,
    EditTypeSnapshot? EditType,
    bool HasRetryOnConcurrency,
    int? RetryMaxAttempts,
    string? RetryOperation,
    bool HasBodyProperties)
{
    internal static CommandCoreModel Create(CommandHandlerInformation information)
    {
        var methodReturnType = TypeSnapshot.Create(information.MethodReturnType);
        var returnModel = information.ReturnModel ?? ReturnModel.Create(methodReturnType);
        return new CommandCoreModel(
            TypeSnapshot.Create(information.ModelType),
            information.HasWithValidateModel,
            information.HasWithDecorators,
            information.MethodName,
            information.MethodIsAsync,
            information.HandlerMustBeAsync,
            methodReturnType,
            TypeSnapshot.Create(information.HandlerReturnType),
            returnModel,
            new EquatableArray<ParameterModel>(information.Parameters.Select(parameter =>
                ParameterModel.Create(
                    parameter,
                    information.ParameterBindings is not null &&
                    information.ParameterBindings.TryGetValue(parameter.Name, out var bindings)
                        ? bindings
                        : default))),
            information.HandlerInterfaceName,
            information.HandlerImplementationName,
            new EquatableArray<string>(information.NotNullProperties),
            information.HasWithUnitOfWork,
            information.HasWithFindEntities,
            information.ContextAccessorType is null ? null : TypeSnapshot.Create(information.ContextAccessorType),
            information.ContextAccessorMode,
            new EquatableArray<IdPropertyBindingSnapshot>(
                information.IdPropertiesBindings.Select(IdPropertyBindingSnapshot.Create)),
            new EquatableArray<string>(information.ProduceProblems),
            information.ProduceNewEntityType is null ? null : TypeSnapshot.Create(information.ProduceNewEntityType),
            information.EditType is null ? null : EditTypeSnapshot.Create(information.EditType),
            information.HasRetryOnConcurrency,
            information.RetryMaxAttempts,
            information.RetryOperation,
            information.HasBodyProperties);
    }

    internal CommandHandlerInformation ToInformation() => new()
    {
        ModelType = PipelineModelConversions.ToDescriptor(ModelType),
        HasWithValidateModel = HasWithValidateModel,
        HasWithDecorators = HasWithDecorators,
        MethodName = MethodName,
        MethodIsAsync = MethodIsAsync,
        HandlerMustBeAsync = HandlerMustBeAsync,
        MethodReturnType = PipelineModelConversions.ToLegacyMethodReturn(Return),
        HandlerReturnType = PipelineModelConversions.ToDescriptor(HandlerReturnType),
        ReturnModel = Return,
        Parameters = Parameters.Select(parameter => PipelineModelConversions.ToDescriptor(parameter.Snapshot)).ToList(),
        ParameterBindings = Parameters
            .Where(parameter => !parameter.Bindings.IsEmpty)
            .ToDictionary(parameter => parameter.Snapshot.Name, parameter => parameter.Bindings, StringComparer.Ordinal),
        HandlerInterfaceName = HandlerInterfaceName,
        HandlerImplementationName = HandlerImplementationName,
        NotNullProperties = NotNullProperties.ToList(),
        HasWithUnitOfWork = HasWithUnitOfWork,
        HasWithFindEntities = HasWithFindEntities,
        ContextAccessorType = ContextAccessorType is null ? null : PipelineModelConversions.ToDescriptor(ContextAccessorType),
        ContextAccessorMode = ContextAccessorMode,
        IdPropertiesBindings = IdPropertiesBindings.Select(PipelineModelConversions.ToDescriptor).ToList(),
        ProduceProblems = ProduceProblems.ToList(),
        ProduceNewEntityType = ProduceNewEntityType is null ? null : PipelineModelConversions.ToDescriptor(ProduceNewEntityType),
        EditType = EditType is null ? null : PipelineModelConversions.ToDescriptor(EditType),
        MapInformation = null,
        HasRetryOnConcurrency = HasRetryOnConcurrency,
        RetryMaxAttempts = RetryMaxAttempts,
        RetryOperation = RetryOperation,
        HasBodyProperties = HasBodyProperties,
    };
}

internal sealed record CommandModel(CommandCoreModel Core, CommandEndpointModel? Endpoint)
{
    internal static CommandModel Create(CommandHandlerInformation information)
    {
        var core = CommandCoreModel.Create(information);
        return new CommandModel(
            core,
            information.MapInformation is null
                ? null
                : CommandEndpointModel.Create(information.MapInformation, core));
    }
}

internal sealed record MapCreatedModel(string RoutePattern, EquatableArray<string> PropertyNames);

internal sealed record MapResponseValuesModel(EquatableArray<PropertySnapshot> Properties);

internal sealed record CommandEndpointModel(
    CommandCoreModel Command,
    string HttpMethod,
    string RoutePattern,
    string EndpointName,
    string? GroupName,
    string? Description,
    string? Summary,
    MapCreatedModel? Created,
    TypeSnapshot? IdResultValueType,
    MapResponseValuesModel? ResponseValues,
    bool RequiresAuthorization,
    EquatableArray<string> AuthorizationPolicies,
    LocationModel NameLocation,
    string? EditRouteParameterName) : IMapEndpointModel
{
    public string? Group => GroupName;

    public string SortKey => $"{GroupName}\u001f{EndpointName}\u001f{HttpMethod}\u001f{RoutePattern}";

    public IMapEndpointGenerator ToGenerator() =>
        new CommandMapEndpointGenerator(ToInformation(), Command.ToInformation(), Command.Return);

    bool IEquatable<IMapEndpointModel>.Equals(IMapEndpointModel? other) =>
        other is CommandEndpointModel model && Equals(model);

    internal static CommandEndpointModel Create(MapInformation information, CommandCoreModel command) => new(
        command,
        information.HttpMethod,
        information.RoutePattern,
        information.EndpointName,
        information.GroupName,
        information.Description,
        information.Summary,
        information.CreatedInformation is null
            ? null
            : new MapCreatedModel(
                information.CreatedInformation.RoutePattern,
                new EquatableArray<string>(information.CreatedInformation.PropertiesNames)),
        information.IdResultValueType is null ? null : TypeSnapshot.Create(information.IdResultValueType),
        information.ResponseValues is null
            ? null
            : new MapResponseValuesModel(new EquatableArray<PropertySnapshot>(
                information.ResponseValues.PropertiesNames.Select(PropertySnapshot.Create))),
        information.AuthorizationPolicies is not null,
        new EquatableArray<string>(information.AuthorizationPolicies),
        LocationModel.Create(information.EndpointNameLocation),
        information.EditRouteParameterName);

    internal MapInformation ToInformation()
    {
        var map = new MapInformation
        {
            HttpMethod = HttpMethod,
            RoutePattern = RoutePattern,
            EndpointName = EndpointName,
            GroupName = GroupName,
            Description = Description,
            Summary = Summary,
            CreatedInformation = Created is null
                ? null
                : new MapCreatedInformation(Created.RoutePattern, Created.PropertyNames.ToArray()),
            IdResultValueType = IdResultValueType is null
                ? null
                : PipelineModelConversions.ToDescriptor(IdResultValueType),
            ResponseValues = ResponseValues is null
                ? null
                : new MapResponseValuesInformation(
                    ResponseValues.Properties.Select(PipelineModelConversions.ToDescriptor).ToList()),
            AuthorizationPolicies = RequiresAuthorization ? AuthorizationPolicies.ToArray() : null,
            EditRouteParameterName = EditRouteParameterName,
        };
        return map;
    }
}

internal interface IMapEndpointModel : IEquatable<IMapEndpointModel>
{
    string? Group { get; }

    string EndpointName { get; }

    LocationModel NameLocation { get; }

    string SortKey { get; }

    IMapEndpointGenerator ToGenerator();
}

internal sealed record FindModel(
    TypeSnapshot EntityType,
    TypeSnapshot IdType,
    TypeSnapshot ModelType,
    string EndpointRoutePattern,
    string EndpointName,
    string? Description,
    string? Summary,
    bool RequiresAuthorization,
    EquatableArray<string> AuthorizationPolicies,
    string? GroupName,
    LocationModel NameLocation) : IMapEndpointModel
{
    public string? Group => GroupName;

    public string SortKey => $"{GroupName}\u001f{EndpointName}\u001fFind\u001f{EndpointRoutePattern}";

    bool IEquatable<IMapEndpointModel>.Equals(IMapEndpointModel? other) =>
        other is FindModel model && Equals(model);

    internal static FindModel Create(FindInformation information) => new(
        TypeSnapshot.Create(information.EntityType),
        TypeSnapshot.Create(information.IdType),
        TypeSnapshot.Create(information.ModelType),
        information.EndpointRoutePattern,
        information.EndpointName,
        information.Description,
        information.Summary,
        information.AuthorizationPolicies is not null,
        new EquatableArray<string>(information.AuthorizationPolicies),
        information.GroupName,
        LocationModel.Create(information.EndpointNameLocation));

    public IMapEndpointGenerator ToGenerator() => new FindInformation(
        PipelineModelConversions.ToDescriptor(EntityType),
        PipelineModelConversions.ToDescriptor(IdType),
        PipelineModelConversions.ToDescriptor(ModelType),
        EndpointRoutePattern,
        EndpointName,
        Description,
        Summary,
        RequiresAuthorization ? AuthorizationPolicies.ToArray() : null,
        GroupName);
}

internal sealed record SearchFilterParameterModel(
    bool HasWithParameterAttribute,
    ParameterModel Parameter,
    bool IsCriteriaParameter,
    bool IsCancellationTokenParameter,
    bool IsHttpContextParameter,
    EquatableArray<ParameterBindingModel> Bindings);

internal sealed record SearchFilterModel(
    string MethodName,
    bool IsAsync,
    EquatableArray<SearchFilterParameterModel> Parameters);

internal sealed record SearchModel(
    TypeSnapshot EntityType,
    TypeSnapshot? SelectType,
    TypeSnapshot FilterType,
    string EndpointRoutePattern,
    string EndpointName,
    string? Description,
    string? Summary,
    bool RequiresAuthorization,
    EquatableArray<string> AuthorizationPolicies,
    string GroupName,
    SearchFilterModel? Filter,
    LocationModel NameLocation) : IMapEndpointModel
{
    public string? Group => GroupName;

    public string SortKey => $"{GroupName}\u001f{EndpointName}\u001fSearch\u001f{EndpointRoutePattern}";

    bool IEquatable<IMapEndpointModel>.Equals(IMapEndpointModel? other) =>
        other is SearchModel model && Equals(model);

    internal static SearchModel Create(SearchInformation information) => new(
        TypeSnapshot.Create(information.EntityType),
        information.SelectType is null ? null : TypeSnapshot.Create(information.SelectType),
        TypeSnapshot.Create(information.FilterType),
        information.EndpointRoutePattern,
        information.EndpointName,
        information.Description,
        information.Summary,
        information.AuthorizationPolicies is not null,
        new EquatableArray<string>(information.AuthorizationPolicies),
        information.GroupName,
        information.Filter is null
            ? null
            : new SearchFilterModel(
                information.Filter.MethodName,
                information.Filter.IsAsync,
                new EquatableArray<SearchFilterParameterModel>(information.Filter.Parameters.Select(parameter =>
                    new SearchFilterParameterModel(
                        parameter.HasWithParameterAttribute,
                        ParameterModel.Create(parameter.ParameterDescriptor),
                        parameter.IsCriteriaParameter,
                        parameter.IsCancellationTokenParameter,
                        parameter.IsHttpContextParameter,
                        parameter.Bindings)))),
        LocationModel.Create(information.EndpointNameLocation));

    public IMapEndpointGenerator ToGenerator() => new SearchInformation(
        PipelineModelConversions.ToDescriptor(EntityType),
        SelectType is null ? null : PipelineModelConversions.ToDescriptor(SelectType),
        PipelineModelConversions.ToDescriptor(FilterType),
        EndpointRoutePattern,
        EndpointName,
        Description,
        Summary,
        RequiresAuthorization ? AuthorizationPolicies.ToArray() : null,
        GroupName,
        Filter is null
            ? null
            : new SearchFilterInformation(
                Filter.MethodName,
                Filter.IsAsync,
                Filter.Parameters.Select(parameter => new SearchFilterParameterInformation(
                    parameter.HasWithParameterAttribute,
                    PipelineModelConversions.ToDescriptor(parameter.Parameter.Snapshot),
                    parameter.IsCriteriaParameter,
                    parameter.IsCancellationTokenParameter,
                    parameter.IsHttpContextParameter,
                    parameter.Bindings)).ToArray()));
}

internal sealed record AddServicesModel(TypeSnapshot ClassType, string Title)
{
    internal static AddServicesModel Create(AddHandlersServicesInformation information) =>
        new(TypeSnapshot.Create(information.ClassType), information.Title);

    internal AddHandlersServicesInformation ToInformation() =>
        new(PipelineModelConversions.ToDescriptor(ClassType), Title, []);
}

internal sealed record MapHostModel(TypeSnapshot ClassType, bool WithOpenApi, LocationModel HostLocation)
{
    internal static MapHostModel Create(MapApiHandlersInformation information) =>
        new(TypeSnapshot.Create(information.ClassType),
            information.WithOpenApi,
            LocationModel.Create(information.HostLocation));

    internal MapApiHandlersInformation ToInformation() =>
        new(PipelineModelConversions.ToDescriptor(ClassType), WithOpenApi, null);
}

internal static class PipelineModelConversions
{
    internal static string? MetadataIdentity(TypeSnapshot snapshot)
        => snapshot.HasCompleteShape
            ? snapshot.OriginalDefinitionQualifiedMetadataName
            : null;

    // Ponte exclusiva para os emitters legados. Toda classificação semântica deve ocorrer antes desta conversão:
    // o descritor reconstruído não possui Symbol e, portanto, não pode usar IsCollection, CreateProperties ou
    // qualquer outra operação que dependa de Roslyn. Os emitters podem consumir apenas forma textual, namespaces,
    // nulabilidade e os papéis restaurados pelos overloads abaixo.
    internal static TypeDescriptor ToDescriptor(TypeSnapshot snapshot) => new(
        snapshot.Name,
        snapshot.Namespaces.ToArray(),
        symbol: null,
        snapshot.IsNullable,
        snapshot.IsNullableReference
            ? NullableAnnotation.Annotated
            : snapshot.IsNonNullableReference
                ? NullableAnnotation.NotAnnotated
                : NullableAnnotation.None);

    internal static TypeDescriptor ToLegacyMethodReturn(ReturnModel model)
    {
        var identity = MetadataIdentity(model.DeclaredType);
        if (identity is not "System.Threading.Tasks.ValueTask" and not "System.Threading.Tasks.ValueTask`1")
            return ToDescriptor(model.DeclaredType);

        if (model.DeclaredType.TypeArguments.Count == 0)
            return new TypeDescriptor("Task", ["System.Threading.Tasks"]);

        var valueType = model.DeclaredType.TypeArguments[0];
        return new TypeDescriptor(
            $"Task<{valueType.Name}>",
            ["System.Threading.Tasks", .. valueType.Namespaces]);
    }

    internal static ParameterDescriptor ToDescriptor(ParameterSnapshot snapshot)
    {
        var type = ToDescriptor(snapshot.TypeUsage.Type);
        if (snapshot.TypeUsage.IsEntity)
            type.MarkAsEntity();
        if (snapshot.TypeUsage.IsContext)
            type.MarkAsContext();
        if (snapshot.TypeUsage.IsHandlerParameter)
            type.MarkAsHandlerParameter();
        if (snapshot.TypeUsage.IsCollectionOfEntities)
            type.MarkAsCollectionOfEntities();
        return new ParameterDescriptor(type, snapshot.Name);
    }

    internal static PropertyDescriptor ToDescriptor(PropertySnapshot snapshot) =>
        new(ToDescriptor(snapshot.Type), snapshot.Name, null);

    internal static EditTypeDescriptor ToDescriptor(EditTypeSnapshot snapshot) => new(
        ToDescriptor(snapshot.EntityType),
        ToDescriptor(snapshot.IdType))
    {
        Parameter = snapshot.Parameter is null ? null : ToDescriptor(snapshot.Parameter),
    };

    internal static IdPropertyBoundToEntityParameter ToDescriptor(IdPropertyBindingSnapshot snapshot) =>
        new(ToDescriptor(snapshot.Parameter), ToDescriptor(snapshot.Property));
}
