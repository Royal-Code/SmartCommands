using Microsoft.CodeAnalysis;
using RoyalCode.SmartCommands.Generators.Models.Descriptors;

namespace RoyalCode.SmartCommands.Generators.Generators;

[Generator]
public class CommandsIncrementalGenerator : IIncrementalGenerator
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
            model.Generator?.Generate(context);

            if (model.Diagnostics is not null)
                context.ReportDiagnostic(model.Diagnostics);
        });

        context.RegisterSourceOutput(pipelineAddServices.Combine(pipelineCollectCommands), static (context, source) =>
        {
            var (addServices, models) = source;

            if (addServices.Diagnostics is not null)
                context.ReportDiagnostic(addServices.Diagnostics);

            if (addServices.Generator is null)
                return;

            var services = models
                .Where(m => m.Generator is not null)
                .Select(m =>
                {
                    var interfaceType = new TypeDescriptor(m.Generator!.HandlerInterfaceName, [m.Generator.Namespace]);
                    var handlerType = new TypeDescriptor(m.Generator.HandlerImplementationName,
                        [$"{m.Generator.Namespace}.Internals"]);
                    return new ServiceTypeDescriptor(interfaceType, handlerType);
                })
                .ToList();

            AddHandlersServicesGenerator.Generate(context, addServices.Generator, services);
        });

        context.RegisterSourceOutput(pipelineMapApiHandlers.Collect().Combine(pipelineCollectCommands),
            static (context, source) =>
            {
                var (mapApiHandlers, models) = source;

                if (mapApiHandlers.Length is 0)
                    return;

                var handler = mapApiHandlers.FirstOrDefault(m => m.Generator is not null);

                if (handler.Generator is null)
                {
                    foreach (var mah in mapApiHandlers)
                    {
                        if (mah.Diagnostics is not null)
                            context.ReportDiagnostic(mah.Diagnostics);
                    }

                    return;
                }

                var mapInformation = models.Where(m => m.Generator?.MapInformation is not null)
                    .Select(m => m.Generator!)
                    .ToList();

                MapApiHandlersGenerator.Generate(context, handler.Generator, mapInformation);
            });
    }
}