using Microsoft.CodeAnalysis;

namespace RoyalCode.SmartCommands.Generators.Generators;

[Generator]
internal class IncrementalGenerator : IIncrementalGenerator
{
    /// <summary>
    /// Tracking names for the incremental pipeline steps, used by the tests to assert caching and
    /// symbol-free retention. Naming a step is required for it to appear in <c>TrackedSteps</c>.
    /// </summary>
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

        /// <summary>
        /// Model-bearing boundaries whose outputs may be retained by the incremental cache. Roslyn's internal
        /// syntax-provider steps are deliberately excluded because their outputs naturally contain Roslyn objects.
        /// </summary>
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
        var pipelineCommands = context.SyntaxProvider.ForAttributeWithMetadataName(
            fullyQualifiedMetadataName: CommandHandlerGenerator.CommandAttributeName,
            predicate: CommandHandlerGenerator.Predicate,
            transform: CommandHandlerGenerator.Transform)
            .WithTrackingName(TrackingNames.Commands);

        var pipelineAddServices = context.SyntaxProvider.ForAttributeWithMetadataName(
            fullyQualifiedMetadataName: AddHandlersServicesGenerator.AddHandlersServicesAttributeName,
            predicate: AddHandlersServicesGenerator.Predicate,
            transform: AddHandlersServicesGenerator.TransformAddServices)
            .WithTrackingName(TrackingNames.AddServices);

        var pipelineMapApiHandlers = context.SyntaxProvider.ForAttributeWithMetadataName(
            fullyQualifiedMetadataName: MapApiHandlersGenerator.AddHandlersServicesAttributeName,
            predicate: MapApiHandlersGenerator.Predicate,
            transform: MapApiHandlersGenerator.TransformMapHandlers)
            .WithTrackingName(TrackingNames.MapApiHandlers);

        var pipelineFindCommands = context.SyntaxProvider.ForAttributeWithMetadataName(
            fullyQualifiedMetadataName: FindGenerator.FindAttributeName,
            predicate: FindGenerator.Predicate,
            transform: FindGenerator.Transform)
            .WithTrackingName(TrackingNames.Finds);

        var pipelineSearchCommands = context.SyntaxProvider.ForAttributeWithMetadataName(
            fullyQualifiedMetadataName: SearchGenerator.SearchAttributeName,
            predicate: SearchGenerator.Predicate,
            transform: SearchGenerator.Transform)
            .WithTrackingName(TrackingNames.Searches);

        var pipelineCollectCommands = pipelineCommands.Collect()
            .WithTrackingName(TrackingNames.CollectedCommands);
        var pipelineCollectFinds = pipelineFindCommands.Collect()
            .WithTrackingName(TrackingNames.CollectedFinds);
        var pipelineCollectSearches = pipelineSearchCommands.Collect()
            .WithTrackingName(TrackingNames.CollectedSearches);

        // gerador dos comandos
        context.RegisterSourceOutput(pipelineCommands, static (context, model) =>
        {
            model.Generate(context);
        });

        // gerador do AddHandlersServices
        var pipelineAddServicesWithCommands = pipelineAddServices.Combine(pipelineCollectCommands)
            .WithTrackingName(TrackingNames.AddServicesWithCommands);

        context.RegisterSourceOutput(pipelineAddServicesWithCommands, static (context, source) =>
        {
            var (addServices, models) = source;

            var services = models
                .Select(m =>
                {
                    var interfaceType = new TypeDescriptor(m.HandlerInterfaceName, [m.Namespace]);
                    var handlerType = new TypeDescriptor(m.HandlerImplementationName,
                        [$"{m.Namespace}.Internals"]);
                    
                    return new AddServiceDescriptor(
                        new ServiceTypeDescriptor(interfaceType, handlerType),
                        m.ContextAccessorMode);
                })
                .ToList();

            addServices.Generate(context, services);
        });

        // combinação dos comandos, finds e searches para gerar os MapInformation
        var pipelineMapInformation = pipelineCollectCommands
            .Combine(pipelineCollectFinds)
            .Select((source, ct) =>
            {
                var (commands, finds) = source;

                return commands
                    .Where(m => m.MapInformation is not null)
                    .Select(m => (IMapEndpointGenerator)m.MapInformation!)
                    .Concat(finds);
            })
            .Combine(pipelineCollectSearches)
            .Select((source, ct) =>
            {
                var (mapEndpoints, searches) = source;
                return mapEndpoints
                    .Concat(searches)
                    .ToList();
            })
            .WithTrackingName(TrackingNames.MapInformation);

        // gerador dos MapApiHandlers
        var pipelineCollectMapApiHandlers = pipelineMapApiHandlers.Collect()
            .WithTrackingName(TrackingNames.CollectedMapApiHandlers);
        var pipelineMapApiHandlersWithMapInformation = pipelineCollectMapApiHandlers.Combine(pipelineMapInformation)
            .WithTrackingName(TrackingNames.MapApiHandlersWithMapInformation);

        context.RegisterSourceOutput(pipelineMapApiHandlersWithMapInformation,
            static (context, source) =>
            {
                var (mapApiHandlers, mapInformation) = source;

                if (mapApiHandlers.Length is 0)
                    return;

                if (mapApiHandlers.Length > 1)
                {
                    foreach (var mah in mapApiHandlers)
                    {
                        context.ReportDiagnostic(Diagnostic.Create(CmdDiagnostics.MultiplesMapApiHandlers, null));
                    }
                }

                mapApiHandlers.First().Generate(context, mapInformation);
            });
    }
}
