using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace RoyalCode.SmartCommands.Generators.Generators;

public static class MapApiHandlersGenerator
{
    public const string AddHandlersServicesAttributeName = "RoyalCode.SmartCommands.MapApiHandlersAttribute";

    public static bool Predicate(SyntaxNode node, CancellationToken token) => node is ClassDeclarationSyntax;

    public static MapApiHandlersInformation TransformMapHandlers(
        GeneratorAttributeSyntaxContext context,
        CancellationToken token)
    {
        var classSyntax = (ClassDeclarationSyntax)context.TargetNode;

        var errors = new List<Diagnostic>();

        // a classe deve ser partial
        if (!classSyntax.Modifiers.Any(SyntaxKind.PartialKeyword))
        {
            var diagnostic = Diagnostic.Create(CmdDiagnostics.InvalidMapApiHandlers,
                location: classSyntax.Identifier.GetLocation(),
                "The class with MapHandlersAttribute must be partial");

            errors.Add(diagnostic);
        }

        // a classe deve ser static
        if (!classSyntax.Modifiers.Any(SyntaxKind.StaticKeyword))
        {
            var diagnostic = Diagnostic.Create(CmdDiagnostics.InvalidMapApiHandlers,
                location: classSyntax.Identifier.GetLocation(),
                "The class with MapHandlersAttribute must be static");

            errors.Add(diagnostic);
        }

        var handlerType = new TypeDescriptor(classSyntax.Identifier.Text, [classSyntax.GetNamespace()]);
        return new MapApiHandlersInformation(handlerType, errors);
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

            // a classe terá um método estático que mapeará os handlers
            var (classGenerator, methodGenerator) = CreateGroupClassAndMethod(
                className: $"Map{className}",
                classNamespace: left.ClassType.Namespaces[0],
                methodName: $"Map{safeGroupName}Group");

            // comando que cria o group
            // deve gerar algo como: var group = builder.MapGroup("MyGroup")
            var assigment = new AssignValueCommand(
                new StringValueNode("var group"),
                new StringValueNode($"builder.MapGroup(\"{groupName}\")"))
            {
                AppendLine = true
            };
            methodGenerator.Commands.Add(assigment);

            // Para cada comando, será gerado um método que chamará o handler
            // para o método que mapeia o handlers, será criado um comando de mapeamento.
            foreach (var mapInformation in group)
            {
                mapInformation.Generate(spc, methodGenerator.Commands, classGenerator.Methods);
            }

            // por fim, finaliza o método retornando o group
            var returnCommand = new ReturnCommand(new StringValueNode("group"));
            methodGenerator.Commands.Add(returnCommand);

            // finaliza, gera a classe
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