using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using RoyalCode.Extensions.SourceGenerator.Diagnostics;
using RoyalCode.Extensions.SourceGenerator.Generation;
using RoyalCode.SmartCommands.Generators.Models;

namespace RoyalCode.SmartCommands.Generators.Generators;

internal static class MapApiHandlersGenerator
{
    public const string AddHandlersServicesAttributeName = "RoyalCode.SmartCommands.MapApiHandlersAttribute";

    public static bool Predicate(SyntaxNode node, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        return node is ClassDeclarationSyntax;
    }

    public static GenerationCandidate<MapHostModel> TransformMapHandlers(
        GeneratorAttributeSyntaxContext context,
        CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        var information = TransformWorking(context, token);
        var diagnostics = PipelineDiagnostic.Snapshot(information.Diagnostics);
        return diagnostics.IsEmpty
            ? GenerationCandidate<MapHostModel>.Valid(MapHostModel.Create(information))
            : GenerationCandidate<MapHostModel>.Invalid(diagnostics);
    }

    private static MapApiHandlersInformation TransformWorking(
        GeneratorAttributeSyntaxContext context,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var classSyntax = (ClassDeclarationSyntax)context.TargetNode;
        var classSymbol = (INamedTypeSymbol)context.TargetSymbol;

        var errors = new List<DiagnosticInfo>();

        // a classe deve ser partial (fato sintático; não há equivalente no símbolo)
        if (!classSyntax.Modifiers.Any(SyntaxKind.PartialKeyword))
        {
            var diagnostic = DiagnosticInfo.Create(CmdDiagnostics.InvalidMapApiHandlers,
                location: classSyntax.Identifier.GetLocation(),
                "The class with MapHandlersAttribute must be partial");

            errors.Add(diagnostic);
        }

        // a classe deve ser static (decisão pelo símbolo)
        if (!classSymbol.IsStatic)
        {
            var diagnostic = DiagnosticInfo.Create(CmdDiagnostics.InvalidMapApiHandlers,
                location: classSyntax.Identifier.GetLocation(),
                "The class with MapHandlersAttribute must be static");

            errors.Add(diagnostic);
        }

        // verifica se a classe tem o atributo WithOpenApi (identificação semântica)
        var withOpenApi = KnownAttributes.Has(classSymbol, KnownAttributes.WithOpenApi);
        var hostAttribute = context.Attributes
            .Select(attribute => attribute.ApplicationSyntaxReference?.GetSyntax(cancellationToken))
            .OfType<AttributeSyntax>()
            .FirstOrDefault();

        var handlerType = TypeDescriptor.Create((ITypeSymbol)context.TargetSymbol);
        return new MapApiHandlersInformation(handlerType, withOpenApi, errors)
        {
            HostLocation = hostAttribute?.GetLocation() ?? classSyntax.Identifier.GetLocation(),
        };
    }

    public static void Generate(
        SourceProductionContext spc,
        MapApiHandlersInformation left,
        IEnumerable<IMapEndpointGenerator> right)
    {
        // Api's agrupadas
        var commandGroup = right.GroupBy(m => m.GroupName);

        // para cada grupo, deve ser criado uma classe como, por exemplo: MapMeuGrupoApi
        foreach (var group in commandGroup)
        {
            var groupName = group.Key ?? left.ClassType.Name;
            var safeGroupName = groupName.ToPascalCase();
            var className = safeGroupName.EndsWith("Api")
                ? safeGroupName
                : $"{safeGroupName}Api";

            // a classe terá um método estático que mapeará os handlers;
            // o hint name usa o nome completo (namespace + tipo)
            var (classGenerator, methodGenerator) = CreateGroupClassAndMethod(
                className: $"Map{className}",
                classNamespace: left.ClassType.Namespaces[0],
                methodName: $"Map{safeGroupName}Group");
            var hintIdentity = $"{left.ClassType.Namespaces[0]}.Map{className}";
            classGenerator.FileName = HintName.Create(hintIdentity, $"Map{className}");

            // comando que cria o group
            // deve gerar algo como: var group = builder.MapGroup("MyGroup")
            var assignment = new AssignValueCommand(
                new StringValueNode("var group"),
                new StringValueNode($"builder.MapGroup({SymbolDisplay.FormatLiteral(groupName, quote: true)})"))
            {
                AppendLine = true
            };
            methodGenerator.Commands.Add(assignment);

            // Para cada comando, será gerado um método que chamará o handler
            // para o método que mapeia o handlers, será criado um comando de mapeamento.
            foreach (var mapInformation in group)
            {
                mapInformation.Generate(spc, methodGenerator.Commands, classGenerator.Methods, left.WithOpenApi);
            }

            // por fim, finaliza o método retornando o group
            var returnCommand = new ReturnCommand(new StringValueNode("group"));
            methodGenerator.Commands.Add(returnCommand);

            // finaliza, gera a classe
            GeneratedFileHeader.AddTo(classGenerator);
            classGenerator.Generate(spc);
        }
    }

    private static (ClassGenerator, MethodGenerator) CreateGroupClassAndMethod(
        string className, string classNamespace, string methodName)
    {
        var classGenerator = new ClassGenerator(className, classNamespace);
        classGenerator.Modifiers.Public();
        classGenerator.Modifiers.Static();
        classGenerator.Modifiers.Partial();

        var endpointRouteBuilder = new TypeDescriptor("IEndpointRouteBuilder", ["Microsoft.AspNetCore.Routing", "Microsoft.AspNetCore.Builder", "Microsoft.AspNetCore.Http"]);
        var routeGroupBuilder = new TypeDescriptor("RouteGroupBuilder", ["Microsoft.AspNetCore.Routing"]);

        var mapMethod = new MethodGenerator(methodName, routeGroupBuilder);
        mapMethod.Modifiers.Public();
        mapMethod.Modifiers.Static();
        mapMethod.Parameters.Add(
            new ParameterGenerator(new ParameterDescriptor(endpointRouteBuilder, "builder"))
            {
                ThisModifier = true
            });

        classGenerator.Methods.Add(mapMethod);

        return (classGenerator, mapMethod);
    }
}
