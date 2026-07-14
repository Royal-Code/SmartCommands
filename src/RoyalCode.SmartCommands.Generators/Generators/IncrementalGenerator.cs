using Microsoft.CodeAnalysis;

namespace RoyalCode.SmartCommands.Generators.Generators;

[Generator]
internal class IncrementalGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var pipelineCommands = context.SyntaxProvider.ForAttributeWithMetadataName(
            fullyQualifiedMetadataName: CommandHandlerGenerator.CommandAttributeName,
            predicate: CommandHandlerGenerator.Predicate,
            transform: CommandHandlerGenerator.Transform);

        var pipelineAddServices = context.SyntaxProvider.ForAttributeWithMetadataName(
            fullyQualifiedMetadataName: AddHandlersServicesGenerator.AddHandlersServicesAttributeName,
            predicate: AddHandlersServicesGenerator.Predicate,
            transform: AddHandlersServicesGenerator.TransformAddServices);

        var pipelineMapApiHandlers = context.SyntaxProvider.ForAttributeWithMetadataName(
            fullyQualifiedMetadataName: MapApiHandlersGenerator.AddHandlersServicesAttributeName,
            predicate: MapApiHandlersGenerator.Predicate,
            transform: MapApiHandlersGenerator.TransformMapHandlers);

        var pipelineFindCommands = context.SyntaxProvider.ForAttributeWithMetadataName(
            fullyQualifiedMetadataName: FindGenerator.FindAttributeName,
            predicate: FindGenerator.Predicate,
            transform: FindGenerator.Transform);

        var pipelineSearchCommands = context.SyntaxProvider.ForAttributeWithMetadataName(
            fullyQualifiedMetadataName: SearchGenerator.SearchAttributeName,
            predicate: SearchGenerator.Predicate,
            transform: SearchGenerator.Transform);

        var pipelineCollectCommands = pipelineCommands.Collect();
        var pipelineCollectFinds = pipelineFindCommands.Collect();
        var pipelineCollectSearches = pipelineSearchCommands.Collect();

        // gerador dos comandos
        context.RegisterSourceOutput(pipelineCommands, static (context, model) =>
        {
            model.Generate(context);
        });

        // gerador do AddHandlersServices
        context.RegisterSourceOutput(pipelineAddServices.Combine(pipelineCollectCommands), static (context, source) =>
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
            });

        // gerador dos MapApiHandlers
        context.RegisterSourceOutput(pipelineMapApiHandlers.Collect().Combine(pipelineMapInformation),
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