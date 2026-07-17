using Microsoft.CodeAnalysis;
using RoyalCode.Extensions.SourceGenerator.Collections;
using RoyalCode.SmartCommands.Generators.Models;

namespace RoyalCode.SmartCommands.Generators.Generators;

[Generator]
internal class IncrementalGenerator : IIncrementalGenerator
{
    internal static class TrackingNames
    {
        public const string Commands = "Commands";
        public const string AddServices = "AddServices";
        public const string MapApiHandlers = "MapApiHandlers";
        public const string Finds = "Finds";
        public const string Searches = "Searches";
        public const string CollectedCommands = "CollectedCommands";
        public const string CollectedFinds = "CollectedFinds";
        public const string CollectedSearches = "CollectedSearches";
        public const string AddServicesWithCommands = "AddServicesWithCommands";
        public const string MapInformation = "MapInformation";
        public const string CollectedMapApiHandlers = "CollectedMapApiHandlers";
        public const string MapApiHandlersWithMapInformation = "MapApiHandlersWithMapInformation";

        public static IReadOnlyList<string> RetainedModelSteps { get; } = Array.AsReadOnly(new[]
        {
            Commands,
            AddServices,
            MapApiHandlers,
            Finds,
            Searches,
            CollectedCommands,
            CollectedFinds,
            CollectedSearches,
            AddServicesWithCommands,
            MapInformation,
            CollectedMapApiHandlers,
            MapApiHandlersWithMapInformation,
        });
    }

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var commandCandidates = context.SyntaxProvider.ForAttributeWithMetadataName(
            CommandHandlerGenerator.CommandAttributeName,
            CommandHandlerGenerator.Predicate,
            CommandHandlerGenerator.Transform);
        context.RegisterSourceOutput(commandCandidates, static (spc, candidate) =>
            PipelineDiagnostic.Report(spc, candidate.Diagnostics));
        var commands = ValidModels(commandCandidates).WithTrackingName(TrackingNames.Commands);

        var addServicesCandidates = context.SyntaxProvider.ForAttributeWithMetadataName(
            AddHandlersServicesGenerator.AddHandlersServicesAttributeName,
            AddHandlersServicesGenerator.Predicate,
            AddHandlersServicesGenerator.TransformAddServices);
        context.RegisterSourceOutput(addServicesCandidates, static (spc, candidate) =>
            PipelineDiagnostic.Report(spc, candidate.Diagnostics));
        var addServices = ValidModels(addServicesCandidates).WithTrackingName(TrackingNames.AddServices);

        var mapHostCandidates = context.SyntaxProvider.ForAttributeWithMetadataName(
            MapApiHandlersGenerator.AddHandlersServicesAttributeName,
            MapApiHandlersGenerator.Predicate,
            MapApiHandlersGenerator.TransformMapHandlers);
        context.RegisterSourceOutput(mapHostCandidates, static (spc, candidate) =>
            PipelineDiagnostic.Report(spc, candidate.Diagnostics));
        var mapHosts = ValidModels(mapHostCandidates).WithTrackingName(TrackingNames.MapApiHandlers);

        var findCandidates = context.SyntaxProvider.ForAttributeWithMetadataName(
            FindGenerator.FindAttributeName,
            FindGenerator.Predicate,
            FindGenerator.Transform);
        context.RegisterSourceOutput(findCandidates, static (spc, candidate) =>
            PipelineDiagnostic.Report(spc, candidate.Diagnostics));
        var finds = ValidModels(findCandidates).WithTrackingName(TrackingNames.Finds);

        var searchCandidates = context.SyntaxProvider.ForAttributeWithMetadataName(
            SearchGenerator.SearchAttributeName,
            SearchGenerator.Predicate,
            SearchGenerator.Transform);
        context.RegisterSourceOutput(searchCandidates, static (spc, candidate) =>
            PipelineDiagnostic.Report(spc, candidate.Diagnostics));
        var searches = ValidModels(searchCandidates).WithTrackingName(TrackingNames.Searches);

        var collectedCommands = commands.Collect().WithTrackingName(TrackingNames.CollectedCommands);
        var collectedFinds = finds.Collect().WithTrackingName(TrackingNames.CollectedFinds);
        var collectedSearches = searches.Collect().WithTrackingName(TrackingNames.CollectedSearches);

        // Cada comando mantém sua própria saída; somente DI e endpoints são agregados.
        context.RegisterSourceOutput(commands, static (spc, model) =>
            model.Core.ToInformation().Generate(spc));

        var addServicesWithCommands = addServices.Combine(collectedCommands)
            .WithTrackingName(TrackingNames.AddServicesWithCommands);
        context.RegisterSourceOutput(addServicesWithCommands, static (spc, source) =>
        {
            var (registration, commandModels) = source;
            var services = commandModels
                .OrderBy(CommandIdentity, StringComparer.Ordinal)
                .Select(command =>
                {
                    var @namespace = command.Core.ModelType.Namespaces[0];
                    return new AddServiceDescriptor(
                        new ServiceTypeDescriptor(
                            new TypeDescriptor(command.Core.HandlerInterfaceName, [@namespace]),
                            new TypeDescriptor(command.Core.HandlerImplementationName, [$"{@namespace}.Internals"])),
                        command.Core.ContextAccessorMode);
                })
                .ToList();

            registration.ToInformation().Generate(spc, services);
        });

        var mapEndpoints = collectedCommands
            .Combine(collectedFinds)
            .Select(static (source, cancellationToken) =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                var (commandModels, findModels) = source;
                return commandModels
                    .Where(command => command.Endpoint is not null)
                    .Select(command => (IMapEndpointModel)command.Endpoint!)
                    .Concat(findModels.Cast<IMapEndpointModel>());
            })
            .Combine(collectedSearches)
            .Select(static (source, cancellationToken) =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                var (endpoints, searchModels) = source;
                return new EquatableArray<IMapEndpointModel>(endpoints
                    .Concat(searchModels.Cast<IMapEndpointModel>())
                    .OrderBy(endpoint => endpoint.SortKey, StringComparer.Ordinal));
            })
            .WithTrackingName(TrackingNames.MapInformation);

        var collectedMapHosts = mapHosts.Collect().WithTrackingName(TrackingNames.CollectedMapApiHandlers);
        var mapHostsWithEndpoints = collectedMapHosts.Combine(mapEndpoints)
            .WithTrackingName(TrackingNames.MapApiHandlersWithMapInformation);

        context.RegisterSourceOutput(mapHostsWithEndpoints, static (spc, source) =>
        {
            var (hostModels, endpointModels) = source;
            if (hostModels.Length == 0)
                return;

            var orderedHosts = hostModels
                .OrderBy(host => PipelineModelConversions.MetadataIdentity(host.ClassType) ?? host.ClassType.Name,
                    StringComparer.Ordinal)
                .ToArray();

            if (orderedHosts.Length > 1)
            {
                foreach (var host in orderedHosts)
                    spc.ReportDiagnostic(Diagnostic.Create(
                        CmdDiagnostics.MultiplesMapApiHandlers,
                        host.HostLocation.ToLocation()));
            }

            // nomes de endpoint devem ser únicos entre todos os endpoints mapeados (WithName global).
            // Cada ocorrência é reportada na localização do argumento do endpoint name, e os endpoints
            // conflitantes são excluídos da emissão do host (a geração relacionada é bloqueada).
            var duplicateNames = new HashSet<string>(
                endpointModels
                    .GroupBy(endpoint => endpoint.EndpointName, StringComparer.Ordinal)
                    .Where(group => group.Count() > 1)
                    .Select(group => group.Key),
                StringComparer.Ordinal);

            // endpointModels já está ordenado por SortKey; a ordem dos diagnósticos é determinística.
            foreach (var endpoint in endpointModels.Where(endpoint => duplicateNames.Contains(endpoint.EndpointName)))
                spc.ReportDiagnostic(Diagnostic.Create(
                    CmdDiagnostics.DuplicateEndpointName,
                    endpoint.NameLocation.ToLocation(),
                    endpoint.EndpointName));

            var remaining = endpointModels
                .Where(endpoint => !duplicateNames.Contains(endpoint.EndpointName))
                .ToList();

            // prefixos de grupo distintos que normalizam para a mesma classe gerada (Map{Nome}Api)
            // produziriam hint names/tipos duplicados; os grupos conflitantes são diagnosticados
            // (RCCMD046) e excluídos da emissão.
            var hostClassName = orderedHosts[0].ClassType.Name;
            var groupsByClassName = new Dictionary<string, List<string?>>(StringComparer.Ordinal);
            foreach (var groupKey in remaining.Select(endpoint => endpoint.Group).Distinct())
            {
                var groupClassName = EndpointNameRules.GroupClassName(groupKey, hostClassName);
                if (groupClassName is null)
                    continue;

                if (!groupsByClassName.TryGetValue(groupClassName, out var groupKeys))
                    groupsByClassName[groupClassName] = groupKeys = [];
                groupKeys.Add(groupKey);
            }

            var conflictingGroups = new HashSet<string?>(groupsByClassName
                .Where(pair => pair.Value.Count > 1)
                .SelectMany(pair => pair.Value));

            if (conflictingGroups.Count > 0)
            {
                foreach (var endpoint in remaining.Where(endpoint => conflictingGroups.Contains(endpoint.Group)))
                    spc.ReportDiagnostic(Diagnostic.Create(
                        CmdDiagnostics.ConflictingGroupNames,
                        endpoint.NameLocation.ToLocation(),
                        endpoint.Group ?? hostClassName,
                        EndpointNameRules.GroupClassName(endpoint.Group, hostClassName)));

                remaining.RemoveAll(endpoint => conflictingGroups.Contains(endpoint.Group));
            }

            // dois endpoints do mesmo grupo que gerariam o mesmo método handler (ex.: comandos homônimos
            // em namespaces diferentes ou dois MapFind da mesma entidade) produziriam C# inválido;
            // são diagnosticados (RCCMD047) e excluídos da emissão.
            var duplicateHandlerMethods = new HashSet<IMapEndpointModel>(remaining
                .GroupBy(endpoint => $"{endpoint.Group}\u001f{endpoint.HandlerMethodName}", StringComparer.Ordinal)
                .Where(group => group.Count() > 1)
                .SelectMany(group => group));

            if (duplicateHandlerMethods.Count > 0)
            {
                foreach (var endpoint in remaining.Where(duplicateHandlerMethods.Contains))
                    spc.ReportDiagnostic(Diagnostic.Create(
                        CmdDiagnostics.DuplicateEndpointHandlerMethod,
                        endpoint.NameLocation.ToLocation(),
                        endpoint.EndpointName,
                        endpoint.HandlerMethodName));

                remaining.RemoveAll(duplicateHandlerMethods.Contains);
            }

            orderedHosts[0].ToInformation().Generate(
                spc,
                remaining.Select(endpoint => endpoint.ToGenerator()));
        });
    }

    private static IncrementalValuesProvider<TModel> ValidModels<TModel>(
        IncrementalValuesProvider<RoyalCode.Extensions.SourceGenerator.Generation.GenerationCandidate<TModel>> candidates)
        where TModel : class, IEquatable<TModel> =>
        candidates
            .Where(static candidate => candidate.IsValid)
            .Select(static (candidate, cancellationToken) =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                return candidate.Model!;
            });

    private static string CommandIdentity(CommandModel command) =>
        PipelineModelConversions.MetadataIdentity(command.Core.ModelType)
        ?? $"{string.Join(".", command.Core.ModelType.Namespaces)}.{command.Core.ModelType.Name}";
}
