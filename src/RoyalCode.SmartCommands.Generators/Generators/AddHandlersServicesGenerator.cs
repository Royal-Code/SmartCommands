using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using RoyalCode.Extensions.SourceGenerator.Diagnostics;
using RoyalCode.Extensions.SourceGenerator.Generation;
using RoyalCode.SmartCommands.Generators.Models;
using static RoyalCode.SmartCommands.Generators.Generators.CommandHandlerInformation;

namespace RoyalCode.SmartCommands.Generators.Generators;

internal static class AddHandlersServicesGenerator
{
    public const string AddHandlersServicesAttributeName = "RoyalCode.SmartCommands.AddHandlersServicesAttribute";

    public static bool Predicate(SyntaxNode node, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        return node is ClassDeclarationSyntax;
    }

    public static GenerationCandidate<AddServicesModel> TransformAddServices(
        GeneratorAttributeSyntaxContext context,
        CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        var information = TransformWorking(context, token);
        var diagnostics = PipelineDiagnostic.Snapshot(information.Diagnostics);
        return diagnostics.IsEmpty
            ? GenerationCandidate<AddServicesModel>.Valid(AddServicesModel.Create(information))
            : GenerationCandidate<AddServicesModel>.Invalid(diagnostics);
    }

    private static AddHandlersServicesInformation TransformWorking(
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
            var diagnostic = DiagnosticInfo.Create(CmdDiagnostics.InvalidCommandType,
                    location: classSyntax.Identifier.GetLocation(),
                    "The class with AddHandlersServicesAttribute must be partial");

            errors.Add(diagnostic);
        }

        // a classe deve ser static (decisão pelo símbolo)
        if (!classSymbol.IsStatic)
        {
            var diagnostic = DiagnosticInfo.Create(CmdDiagnostics.InvalidCommandType,
                location: classSyntax.Identifier.GetLocation(),
                "The class with AddHandlersServicesAttribute must be static");

            errors.Add(diagnostic);
        }

        // lê o atributo pela identidade semântica; o título vem do TypedConstant, então
        // constantes referenciadas também são aceitas (não apenas literais).
        if (!KnownAttributes.TryGet(classSymbol, KnownAttributes.AddHandlersServices, out var attr)
            || attr!.ConstructorArguments.Length is not 1)
        {
            var diagnostic = DiagnosticInfo.Create(CmdDiagnostics.InvalidCommandType,
                    location: classSyntax.Identifier.GetLocation(),
                    "Problem finding attribute for class with AddHandlersServicesAttribute");

            errors.Add(diagnostic);
        }

        var title = attr?.ConstructorArguments.Length == 1
            ? KnownAttributes.GetString(attr.ConstructorArguments[0])
            : null;

        if (title is null)
        {
            var diagnostic = DiagnosticInfo.Create(CmdDiagnostics.InvalidCommandType,
                    location: classSyntax.Identifier.GetLocation(),
                    "The title for AddHandlersServicesAttribute must be a constant string");

            errors.Add(diagnostic);
        }

        var handlerType = TypeDescriptor.Create((ITypeSymbol)context.TargetSymbol);
        return new AddHandlersServicesInformation(handlerType, title ?? string.Empty, errors);
    }

    public static void Generate(
        SourceProductionContext spc,
        AddHandlersServicesInformation left,
        IEnumerable<AddServiceDescriptor> right)
    {
        var classGenerator = new ClassGenerator(left.ClassType.Name, left.ClassType.Namespaces[0]);
        classGenerator.Modifiers.Public();
        classGenerator.Modifiers.Static();
        classGenerator.Modifiers.Partial();

        // aqui é preciso olhar todos os serviços e verificar os modos genéricos.
        // se houver modos genéricos distintos entre DbContext e WorkContext,
        // deve ser gerado dois tipos genéricos, TDbContext e TWorkContext e o ServiceTypeDescriptor
        // deve ser substituído o tipo genérico por TDbContext ou TWorkContext.
        // Se houver apenas um modo genérico, deve ser usado o TContext.
        // Se não houver modos genéricos, não se usa Generics no método.

        // gera o método AddHandlersServices
        var method = new MethodGenerator($"Add{left.Title}HandlersServices", TypeDescriptor.Void());
        method.Modifiers.Public();
        method.Modifiers.Static();
        method.Parameters.Add(
            new ParameterGenerator(
                new ParameterDescriptor(
                    new TypeDescriptor("IServiceCollection", ["Microsoft.Extensions.DependencyInjection"]),
                   "services"))
            {
                ThisModifier = true
            });

        var hasWorkContextGeneric = right.Any(r => r.ContextAccessorMode == ContextAccessorModes.WorkContext);
        var hasDbContextGeneric = right.Any(r => r.ContextAccessorMode == ContextAccessorModes.DbContext);
        
        var mustOverride = hasWorkContextGeneric && hasDbContextGeneric;

        if (mustOverride)
        {
            method.Generics.AddGeneric("TWorkContext", ["RoyalCode.WorkContext"]);
            method.Where.Add(new WhereGenerator("TWorkContext", "IWorkContext"));
            method.Generics.AddGeneric("TDbContext", ["Microsoft.EntityFrameworkCore"]);
            method.Where.Add(new WhereGenerator("TDbContext", "DbContext"));
        }
        else if (hasWorkContextGeneric)
        {
            method.Generics.AddGeneric("TContext", ["RoyalCode.WorkContext"]);
            method.Where.Add(new WhereGenerator("TContext", "IWorkContext"));
        }
        else if (hasDbContextGeneric)
        {
            method.Generics.AddGeneric("TContext", ["Microsoft.EntityFrameworkCore"]);
            method.Where.Add(new WhereGenerator("TContext", "DbContext"));
        }

        foreach (var std in right)
        {
            var serviceTypeDescriptor = std.ServiceTypeDescriptor;
            if (std.ContextAccessorMode == ContextAccessorModes.DbContext)
            {
                serviceTypeDescriptor = new ServiceTypeDescriptor(
                    serviceTypeDescriptor.InterfaceType,
                    new TypeDescriptor(
                        $"{serviceTypeDescriptor.HandlerType.Name}<{(mustOverride ? "TDbContext" : "TContext")}>",
                        serviceTypeDescriptor.HandlerType.Namespaces));
            }
            else if (std.ContextAccessorMode == ContextAccessorModes.WorkContext)
            {
                serviceTypeDescriptor = new ServiceTypeDescriptor(
                    serviceTypeDescriptor.InterfaceType,
                    new TypeDescriptor(
                        $"{serviceTypeDescriptor.HandlerType.Name}<{(mustOverride ? "TWorkContext" : "TContext")}>",
                        serviceTypeDescriptor.HandlerType.Namespaces));
            }

            method.Commands.Add(new AddServiceCommand(serviceTypeDescriptor, "services"));
        }

        classGenerator.Methods.Add(method);

        var hintIdentity = $"{left.ClassType.Namespaces[0]}.{left.ClassType.Name}_AddHandlersServices";
        classGenerator.FileName = HintName.Create(hintIdentity, $"{left.ClassType.Name}_AddHandlersServices");
        GeneratedFileHeader.AddTo(classGenerator);
        classGenerator.Generate(spc);

    }
}
