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
                    endpoint.EndpointName.Trim('"')));

            orderedHosts[0].ToInformation().Generate(
                spc,
                endpointModels
                    .Where(endpoint => !duplicateNames.Contains(endpoint.EndpointName))
                    .Select(endpoint => endpoint.ToGenerator()));
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
