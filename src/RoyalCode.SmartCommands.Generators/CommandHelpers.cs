using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace RoyalCode.SmartCommands.Generators;

internal static class CommandHelpers
{
    public static bool ValidateReturnType(this MethodDeclarationSyntax method, out Diagnostic? diagnostic)
    {
        // obtém o tipo retornado pelo método
        var returnType = method.ReturnType;

        // Valida se o método retorna algum valor, ou seja, não é Task nem void.
        if (returnType is GenericNameSyntax genericName && 
                genericName.Identifier.Text == "Task" && 
                genericName.TypeArgumentList.Arguments.Count is 0
            || returnType is SimpleNameSyntax simpleName && 
                simpleName.Identifier.Text == "void")
        {
            diagnostic = Diagnostic.Create(
                CmdDiagnostics.InvalidReturnType,
                method.Identifier.GetLocation());
            return false;
        }

        diagnostic = null;
        return true;
    }

    public static bool ValidateClassWithHasProblemsMethod(
        this ClassDeclarationSyntax cls,
        AttributeSyntax attributeNode,
        out MethodDeclarationSyntax? hasProblemsMethod,
        out Diagnostic? diagnostic)
    {
        // Tenta obter o método HasProblems
        hasProblemsMethod = cls.Members
            .OfType<MethodDeclarationSyntax>()
            .FirstOrDefault(m => m.Identifier.Text == "HasProblems");

        if (hasProblemsMethod is null)
        {
            diagnostic = Diagnostic.Create(
                descriptor: CmdDiagnostics.HasProblemsMethodNotFound,
                location: attributeNode.Name.GetLocation(),
                additionalLocations: [cls.Identifier.GetLocation()]
                );
            return false;
        }

        // Valida o retorno do método HasProblems, deve retornar um bool
        if (hasProblemsMethod.ReturnType is not PredefinedTypeSyntax predefinedType
            || predefinedType.Keyword.Text != "bool")
        {
            diagnostic = Diagnostic.Create(
                descriptor: CmdDiagnostics.HasProblemsMethodDoesNotReturnBool,
                location: hasProblemsMethod.Identifier.GetLocation(),
                additionalLocations: [attributeNode.Name.GetLocation()]
                );
            return false;
        }

        // valida o parâmetro, deve ter um, e ser out e do tipo Problems
        var parameters = hasProblemsMethod.ParameterList.Parameters;
        if (parameters.Count != 1)
        {
            diagnostic = Diagnostic.Create(
                descriptor: CmdDiagnostics.HasProblemsMethodDoesNotHaveOutParameterProblems,
                location: hasProblemsMethod.Identifier.GetLocation(),
                additionalLocations: [attributeNode.Name.GetLocation()]
                );
            return false;
        }

        // valida se tem out
        var parameter = parameters[0];
        if (parameter.Modifiers.Count != 1
            || parameter.Modifiers[0].Text != "out")
        {
            diagnostic = Diagnostic.Create(
                descriptor: CmdDiagnostics.HasProblemsMethodDoesNotHaveOutParameterProblems,
                location: hasProblemsMethod.Identifier.GetLocation(),
                additionalLocations: [attributeNode.Name.GetLocation()]
                );
            return false;
        }

        // valida o tipo, que deve ser Problems
        if (parameter.Type is NullableTypeSyntax { ElementType: IdentifierNameSyntax { Identifier.Text: "Problems" } })
        {
            diagnostic = null;
            return true;
        }

        diagnostic = Diagnostic.Create(
            descriptor: CmdDiagnostics.HasProblemsMethodDoesNotHaveOutParameterProblems,
            location: hasProblemsMethod.Identifier.GetLocation(),
            additionalLocations: [attributeNode.Name.GetLocation()]
            );
        return false;
    }

    public static bool ValidateCancellationParameter(this MethodDeclarationSyntax method, out Diagnostic? diagnostic)
    {
        var cancellationToken = method.ParameterList.Parameters.Last();
        if (cancellationToken.Type is not IdentifierNameSyntax { Identifier.Text: "CancellationToken" })
        {
            diagnostic = Diagnostic.Create(
                CmdDiagnostics.InvalidCommandType,
                cancellationToken.Identifier.GetLocation(),
                "CancellationToken must be the last parameter");
            return false;
        }

        diagnostic = null;
        return true;
    }

    public static bool ValidateMapIdResultValue(this TypeSyntax resultType, SemanticModel semanticModel, out Diagnostic? diagnostic)
    {
        // se for task, pega o tipo genérico
        if (resultType is GenericNameSyntax { Identifier.Text: "Task" } genericName)
        {
            resultType = genericName.TypeArgumentList.Arguments[0];
        }

        // se for um Result<T>, pega o tipo genérico
        if (resultType is GenericNameSyntax { Identifier.Text: "Result" } genericName2)
        {
            resultType = genericName2.TypeArgumentList.Arguments[0];
        }

        // verifica se o tipo retornado tem uma propriedade chamada Id
        var typeSymbol = semanticModel.GetTypeInfo(resultType).Type;
        if (typeSymbol is null)
        {
            diagnostic = Diagnostic.Create(
                CmdDiagnostics.InvalidCommandType,
                resultType.GetLocation(),
                "the returned type was not found");
            return false;
        }

        // verifica se tem a propriedade Id ou se possui herança, uma das classes bases tem a propriedade Id
        while (typeSymbol is not null)
        {
            if (typeSymbol.GetMembers("Id").Length == 1)
            {
                diagnostic = null;
                return true;
            }

            typeSymbol = typeSymbol.BaseType;
        }

        diagnostic = Diagnostic.Create(
            CmdDiagnostics.InvalidCommandType,
            resultType.GetLocation(),
            "Type must have a property called Id");
        return false;


    }
}
