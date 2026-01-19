using Microsoft.CodeAnalysis;
namespace RoyalCode.SmartCommands.Generators.Generators;

internal class SearchInformation : IEquatable<SearchInformation>, IMapEndpointGenerator
{
    private readonly List<Diagnostic>? errors;

    public SearchInformation(Diagnostic diagnostic)
    {
        errors = [diagnostic];
    }

    public SearchInformation(List<Diagnostic> diagnostics)
    {
        errors = diagnostics;
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
        string groupName,
        SearchFilterInformation? filter)
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
        Filter = filter;
    }

    public TypeDescriptor EntityType { get; }

    public TypeDescriptor? SelectType { get; }

    public TypeDescriptor FilterType { get; }

    public string EndpointRoutePattern { get; }

    public string EndpointName { get; }

    public string GroupName { get; }

    public string? Description { get; }

    public string? Summary { get; }

    public string[]? AuthorizationPolicies { get; }

    public SearchFilterInformation? Filter { get; set; }

    public bool Equals(SearchInformation other)
    {
        if (other is null)
            return false;

        return ReferenceEquals(this, other) ||
            EntityType.Equals(other.EntityType) &&
            (SelectType is null && other.SelectType is null || SelectType?.Equals(other.SelectType) == true) &&
            FilterType.Equals(other.FilterType) &&
            EndpointRoutePattern == other.EndpointRoutePattern &&
            EndpointName == other.EndpointName &&
            GroupName == other.GroupName &&
            Description == other.Description &&
            Summary == other.Summary &&
            AuthorizationPolicies?.SequenceEqual(other.AuthorizationPolicies ?? []) == true &&
            (Filter is null && other.Filter is null || Filter?.Equals(other.Filter) == true);
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
        hashCode = hashCode * -1521134295 + (AuthorizationPolicies != null ? 
            AuthorizationPolicies.Aggregate(0, (current, policy) => current ^ policy.GetHashCode()) : 0);
        hashCode = hashCode * -1521134295 + (Filter?.GetHashCode() ?? 0);
        return hashCode;
    }

    public void Generate(SourceProductionContext spc, GeneratorNodeList commands, GeneratorNodeList methods, bool withOpenApi)
    {
        if (errors is not null && errors.Count > 0)
        {
            errors.ForEach(spc.ReportDiagnostic);
            return;
        }

        var handlerMethod = GenerateHandlerMethod();
        methods.Add(handlerMethod);

        var mapMethodInvoke = GenerateMapMethodInvoke(withOpenApi);
        var invokeCommand = new Command(mapMethodInvoke);
        commands.Add(invokeCommand);
    }

    private MethodInvokeGenerator GenerateMapMethodInvoke(bool withOpenApi)
    {
        var handlerMethodName = $"Search{EntityType.Name}By{FilterType.Name}Async";

        var methodInvoke = new MethodInvokeGenerator("group", $"MapGet");
        methodInvoke.AddArgument(EndpointRoutePattern);
        methodInvoke.AddArgument(handlerMethodName);

        methodInvoke = new MethodInvokeGenerator(methodInvoke, "WithName", EndpointName)
        {
            LineIdent = true
        };

        if (Description is not null)
        {
            methodInvoke = new MethodInvokeGenerator(methodInvoke, "WithDescription", Description)
            {
                LineIdent = true
            };
        }

        if (Summary is not null)
        {
            methodInvoke = new MethodInvokeGenerator(methodInvoke, "WithSummary", Summary)
            {
                LineIdent = true
            };
        }

        if (AuthorizationPolicies is not null)
        {
            // adiciona os AuthorizationPolicies, se houver
            ArgumentsGenerator arguments = new();
            foreach (var policy in AuthorizationPolicies)
            {
                arguments.AddArgument(policy);
            }
            methodInvoke = new MethodInvokeGenerator(methodInvoke, "RequireAuthorization", arguments)
            {
                LineIdent = true
            };
        }

        // por fim, chama WithOpenApi
        if (withOpenApi)
        {
            methodInvoke = new MethodInvokeGenerator(methodInvoke, "WithOpenApi")
            {
                LineIdent = true
            };
        }

        return methodInvoke;
    }

    private MethodGenerator GenerateHandlerMethod()
    {
        var handlerMethodName = $"Search{EntityType.Name}By{FilterType.Name}Async";

        // 1 - Declaração do método

        // 1.1 - Tipo de retorno

        // return type: Task<MatchSearch<TSelect>>
        var returnType = new TypeDescriptor(
            $"Task<MatchSearch<{SelectType?.Name ?? EntityType.Name}>>",
            [
                "System.Threading.Tasks",
                "RoyalCode.SmartProblems",
                "RoyalCode.SmartSearch", 
                "RoyalCode.SmartSearch.AspNetCore.HttpResults",
                "RoyalCode.SmartSearch.AspNetCore.Internals",
                .. SelectType?.Namespaces ?? EntityType.Namespaces
            ]);

        // 1.2 - Método, nome e modificadores

        var method = new MethodGenerator(handlerMethodName, returnType);
        method.Modifiers.Private();
        method.Modifiers.Static();

        // 1.3 - Atributos

        // atributo produce problems (InvalidParameter, InternalServerError)
        method.Attributes.Add(new AttributeGenerator(
            "ProduceProblems", 
            ["RoyalCode.SmartProblems"],
            "ProblemCategory.InvalidParameter", "ProblemCategory.InternalServerError"));

        // 1.4 - parâmetros

        // adiciona os parâmetros
        method.Parameters.InLine = false;

        // primeiro parâmetro: filter
        var parameter = new ParameterGenerator(new ParameterDescriptor(FilterType, "filter"));
        parameter.Attributes.Add(new AttributeGenerator("AsParameters", ["Microsoft.AspNetCore.Http"]) { InLine = true });
        method.Parameters.Add(parameter);

        // segundo parâmetro: options
        parameter = new ParameterGenerator(new ParameterDescriptor(
            new TypeDescriptor("SearchOptions", ["RoyalCode.SmartSearch"]), "options"));
        parameter.Attributes.Add(new AttributeGenerator("AsParameters", ["Microsoft.AspNetCore.Http"]) { InLine = true });
        method.Parameters.Add(parameter);

        // terceiro parâmetro: orderBy
        parameter = new ParameterGenerator(new ParameterDescriptor(
            new TypeDescriptor("Sorting[]?", ["RoyalCode.SmartSearch"]), "orderby"));
        parameter.Attributes.Add(new AttributeGenerator("FromQuery", ["Microsoft.AspNetCore.Mvc"]) { InLine = true });
        method.Parameters.Add(parameter);

        // quarto parâmetro: criteria
        parameter = new ParameterGenerator(new ParameterDescriptor(
            new TypeDescriptor($"ICriteria<{EntityType.Name}>", ["RoyalCode.SmartSearch", .. EntityType.Namespaces]), "criteria"));
        parameter.Attributes.Add(new AttributeGenerator("FromServices", ["Microsoft.AspNetCore.Mvc"]) { InLine = true });
        method.Parameters.Add(parameter);

        // quinto parâmetro: logger
        parameter = new ParameterGenerator(new ParameterDescriptor(
            new TypeDescriptor($"ILogger<ICriteria<{EntityType.Name}>>", ["Microsoft.Extensions.Logging"]), "logger"));
        parameter.Attributes.Add(new AttributeGenerator("FromServices", ["Microsoft.AspNetCore.Mvc"]) { InLine = true });
        method.Parameters.Add(parameter);

        // parametros adicionais de SearchFilterInformation, caso existam
        if (Filter is not null)
        {
            foreach (var filterParam in Filter.Parameters)
            {
                if (filterParam.IsCriteriaParameter || filterParam.IsCancellationTokenParameter)
                    continue;

                parameter = new ParameterGenerator(new ParameterDescriptor(
                    filterParam.ParameterDescriptor.Type,
                    filterParam.ParameterDescriptor.Name));

                if (filterParam.HasWithParameterAttribute)
                {
                    parameter.Attributes.Add(new AttributeGenerator("FromRoute", ["Microsoft.AspNetCore.Mvc"]) { InLine = true });
                }
                else if (!filterParam.IsHttpContextParameter)
                {
                    parameter.Attributes.Add(new AttributeGenerator("FromServices", ["Microsoft.AspNetCore.Mvc"]) { InLine = true });
                }

                method.Parameters.Add(parameter);
            }
        }

        // último parametro: cancellationToken
        parameter = new ParameterGenerator(new ParameterDescriptor(
            new TypeDescriptor("CancellationToken", ["System.Threading"]), "ct"));
        method.Parameters.Add(parameter);

        // 2 - Corpo do método

        // 2.1 - Cria configure
        var configureVar = new StringValueNode((Filter?.IsAsync ?? false)
            ? $"Func<ICriteria<{EntityType.Name}>, Task>? configure"
            : $"Action<ICriteria<{EntityType.Name}>>? configure");

        ValueNode? configureValue;
        if (Filter is not null)
        {
            var lambda = new LambdaGenerator
            {
                Block = false,
                InLine = true,
                Async = Filter.IsAsync
            };
            lambda.Parameters.AddArgument("criteria");

            var filterInvoke = new MethodInvokeGenerator("filter", Filter.MethodName);
            if (Filter.IsAsync)
                filterInvoke.Await = true;

            foreach (var filterParam in Filter.Parameters)
            {
                if (filterParam.IsCriteriaParameter)
                {
                    filterInvoke.AddArgument("criteria");
                }
                else if (filterParam.IsCancellationTokenParameter)
                {
                    filterInvoke.AddArgument("ct");
                }
                else
                {
                    filterInvoke.AddArgument(filterParam.ParameterDescriptor.Name);
                }
            }

            lambda.Commands.Add(filterInvoke);

            configureValue = lambda;
        }
        else
        {
            configureValue = new StringValueNode("null");
        }

        var configureAssign = new AssignValueCommand(configureVar, configureValue);
        method.Commands.Add(configureAssign);

        // 2.2 - Invoka Performer
        var searchMethodName = SelectType is null 
            ? $"SearchAsync<{EntityType.Name}, {FilterType.Name}>" 
            : $"SearchAsync<{EntityType.Name}, {SelectType.Name}, {FilterType.Name}>";

        var performerInvoke = new MethodInvokeGenerator("Performer", searchMethodName);
        performerInvoke.AddArgument("filter");
        performerInvoke.AddArgument("options");
        performerInvoke.AddArgument("orderby");
        performerInvoke.AddArgument("criteria");
        performerInvoke.AddArgument("configure");
        performerInvoke.AddArgument("logger");
        performerInvoke.AddArgument("ct");

        // 2.3 cria o return

        var returnCommand = new ReturnCommand(performerInvoke);
        method.Commands.Add(returnCommand);

        return method;
    }
}

internal class SearchFilterInformation : IEquatable<SearchFilterInformation>
{
    public SearchFilterInformation(
        string methodName,
        bool isAsync,
        SearchFilterParameterInformation[] parameters)
    {
        MethodName = methodName;
        IsAsync = isAsync;
        Parameters = parameters;
    }

    public string MethodName { get; }

    public bool IsAsync { get; }

    public SearchFilterParameterInformation[] Parameters { get; }

    public bool Equals(SearchFilterInformation? other)
    {
        if (other is null)
            return false;

        return ReferenceEquals(this, other) ||
            MethodName == other.MethodName &&
            IsAsync == other.IsAsync &&
            Parameters.SequenceEqual(other.Parameters);
    }

    public override bool Equals(object obj)
    {
        return obj is SearchFilterInformation other && Equals(other);
    }

    public override int GetHashCode()
    {
        var hashCode = 1521134295;
        hashCode = hashCode * -1521134295 + MethodName.GetHashCode();
        hashCode = hashCode * -1521134295 + IsAsync.GetHashCode();
        hashCode = hashCode * -1521134295 + Parameters.Aggregate(0, (current, param) => current ^ param.GetHashCode());
        return hashCode;
    }
}

internal class SearchFilterParameterInformation : IEquatable<SearchFilterParameterInformation>
{
    public SearchFilterParameterInformation(
        bool hasWithParameterAttribute,
        ParameterDescriptor parameterDescriptor)
    {
        HasWithParameterAttribute = hasWithParameterAttribute;
        ParameterDescriptor = parameterDescriptor;
    }

    public bool IsCriteriaParameter => ParameterDescriptor.Type.Name.StartsWith("ICriteria<");

    public bool IsCancellationTokenParameter => ParameterDescriptor.Type.IsCancellationToken;

    public bool IsHttpContextParameter => ParameterDescriptor.Type.Name == "HttpContext";

    public bool HasWithParameterAttribute { get; }

    public ParameterDescriptor ParameterDescriptor { get; }

    public bool Equals(SearchFilterParameterInformation other)
    {
        if (other is null)
            return false;

        return ReferenceEquals(this, other) ||
            HasWithParameterAttribute == other.HasWithParameterAttribute &&
            ParameterDescriptor.Equals(other.ParameterDescriptor);
    }

    public override bool Equals(object obj)
    {
        return obj is SearchFilterParameterInformation other && Equals(other);
    }

    public override int GetHashCode()
    {
        var hashCode = 1861411795;
        hashCode = hashCode * -1521134295 + HasWithParameterAttribute.GetHashCode();
        hashCode = hashCode * -1521134295 + ParameterDescriptor.GetHashCode();
        return hashCode;
    }
}