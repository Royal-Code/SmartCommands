using Microsoft.CodeAnalysis;
using RoyalCode.SmartCommands.Generators.Models.Descriptors;

namespace RoyalCode.SmartCommands.Generators.Generators;

[Generator]
public class IncrementalGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var pipelineCommands = context.SyntaxProvider.ForAttributeWithMetadataName(
            fullyQualifiedMetadataName: CommandHandlerGenerator.CommandAttributeName,
            predicate: CommandHandlerGenerator.Predicate,
            transform: CommandHandlerGenerator.Transform);

        var pipelineCollectCommands = pipelineCommands.Collect();

        var pipelineAddServices = context.SyntaxProvider.ForAttributeWithMetadataName(
            fullyQualifiedMetadataName: AddHandlersServicesGenerator.AddHandlersServicesAttributeName,
            predicate: AddHandlersServicesGenerator.Predicate,
            transform: AddHandlersServicesGenerator.TransformAddServices);

        var pipelineMapApiHandlers = context.SyntaxProvider.ForAttributeWithMetadataName(
            fullyQualifiedMetadataName: MapApiHandlersGenerator.AddHandlersServicesAttributeName,
            predicate: MapApiHandlersGenerator.Predicate,
            transform: MapApiHandlersGenerator.TransformMapHandlers);

        context.RegisterSourceOutput(pipelineCommands, static (context, model) =>
        {
            model.Generate(context);
        });

        context.RegisterSourceOutput(pipelineAddServices.Combine(pipelineCollectCommands), static (context, source) =>
        {
            var (addServices, models) = source;

            var services = models
                .Select(m =>
                {
                    var interfaceType = new TypeDescriptor(m.HandlerInterfaceName, [m.Namespace]);
                    var handlerType = new TypeDescriptor(m.HandlerImplementationName,
                        [$"{m.Namespace}.Internals"]);
                    return new ServiceTypeDescriptor(interfaceType, handlerType);
                })
                .ToList();

            addServices.Generate(context, services);
        });

        context.RegisterSourceOutput(pipelineMapApiHandlers.Collect().Combine(pipelineCollectCommands),
            static (context, source) =>
            {
                var (mapApiHandlers, models) = source;

                if (mapApiHandlers.Length is 0)
                    return;

                if (mapApiHandlers.Length > 1)
                {
                    foreach(var mah in mapApiHandlers)
                    {
                        context.ReportDiagnostic(Diagnostic.Create(CmdDiagnostics.MultiplesMapApiHandlers, null));
                    }
                }
                
                var handler = mapApiHandlers.First();

                var mapInformation = models
                    .Where(m => m.MapInformation is not null)
                    .ToList();

                handler.Generate(context, mapInformation);
            });
    }
}