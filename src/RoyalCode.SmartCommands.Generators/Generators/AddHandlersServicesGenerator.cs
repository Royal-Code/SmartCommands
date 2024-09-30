using Coreum.NewCommands.Generators.Models;
using Coreum.NewCommands.Generators.Models.Commands;
using Coreum.NewCommands.Generators.Models.Descriptors;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Coreum.NewCommands.Generators.Generators;

public static class AddHandlersServicesGenerator
{
    public const string AddHandlersServicesAttributeName = "Coreum.NewCommands.AddHandlersServicesAttribute";

    public static bool Predicate(SyntaxNode node, CancellationToken token) => node is ClassDeclarationSyntax;

    public static TransformResult<AddHandlersServicesInformation> TransformAddServices(
        GeneratorAttributeSyntaxContext context,
        CancellationToken token)
    {
        var classSyntax = (ClassDeclarationSyntax)context.TargetNode;

        if (!classSyntax.Modifiers.Any(SyntaxKind.PartialKeyword))
        {
            var diagnostic = Diagnostic.Create(CmdDiagnostics.InvalidCommandType,
                    location: classSyntax.Identifier.GetLocation(),
                    "The class with AddHandlersServicesAttribute must be partial");

            return diagnostic;
        }

        if (!classSyntax.Modifiers.Any(SyntaxKind.StaticKeyword))
        {
            var diagnostic = Diagnostic.Create(CmdDiagnostics.InvalidCommandType,
                location: classSyntax.Identifier.GetLocation(),
                "The class with AddHandlersServicesAttribute must be static");

            return diagnostic;
        }

        if (!classSyntax.TryGetAttribute("AddHandlersServices", out var attr)
            || attr?.ArgumentList?.Arguments.Count is not 1)
        {
            var diagnostic = Diagnostic.Create(CmdDiagnostics.InvalidCommandType,
                    location: classSyntax.Identifier.GetLocation(),
                    "Problem finding attribute for class with AddHandlersServicesAttribute");

            return diagnostic;
        }

        var titleExpression = attr?.ArgumentList?.Arguments[0].Expression;

        if (!titleExpression.IsKind(SyntaxKind.StringLiteralExpression))
        {
            var diagnostic = Diagnostic.Create(CmdDiagnostics.InvalidCommandType,
                    location: classSyntax.Identifier.GetLocation(),
                    "The title for AddHandlersServicesAttribute must be a literal string");

            return diagnostic;
        }

        var title = titleExpression.ToString();
        title = title.Substring(1, title.Length - 2);

        var handlerType = new TypeDescriptor(classSyntax.Identifier.Text, [classSyntax.GetNamespace()]);
        return new AddHandlersServicesInformation(handlerType, title);
    }

    public static void Generate(
        SourceProductionContext spc,
        AddHandlersServicesInformation left,
        IEnumerable<ServiceTypeDescriptor> right)
    {
        var classGenerator = new ClassGenerator(left.ClassType.Name, left.ClassType.Namespaces[0]);
        classGenerator.Modifiers.Public();
        classGenerator.Modifiers.Static();
        classGenerator.Modifiers.Partial();

        var method = new MethodGenerator($"Add{left.Title}HandlersServices", TypeDescriptor.Void);
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

        foreach (var std in right)
        {
            method.Commands.Add(new AddServiceCommand(std, "services"));
            classGenerator.Usings.AddNamespaces(std.HandlerType);
            classGenerator.Usings.AddNamespaces(std.InterfaceType);
        }

        classGenerator.Methods.Add(method);
        classGenerator.Usings.AddNamespaces(method);

        classGenerator.FileName = $"{left.ClassType.Name}_AddHandlersServices.g.cs";
        classGenerator.Generate(spc);

    }
}
