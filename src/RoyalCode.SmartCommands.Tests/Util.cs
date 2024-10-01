using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using RoyalCode.SmartCommands.Generators.Generators;
using RoyalCode.SmartCommands.Tests.Models;

namespace RoyalCode.SmartCommands.Tests;

internal static class Util
{
    internal static void Compile(
        string sourceCode,
        out Compilation outputCompilation,
        out ImmutableArray<Diagnostic> diagnostics)
    {
        // the source code to be compiled
        var syntaxTree = CSharpSyntaxTree.ParseText(sourceCode);

        // assemblies references requered to compile the source code
        var references = new List<MetadataReference>
        {
            MetadataReference.CreateFromFile(typeof(Util).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(CommandHandlerGenerator).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(Enumerable).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(ICollection<>).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(CommandAttribute).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(RoyalCode.WorkContext.Abstractions.IWorkContext).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(RoyalCode.SmartProblems.Result).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(Task).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(CancellationToken).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(Produto).Assembly.Location)
        };

        // create a compilation for the source code.
        var compilation = CSharpCompilation.Create("SourceGeneratorTests", [syntaxTree], references, 
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        // apply the source generator and collect the output
        var driver = CSharpGeneratorDriver.Create(new CommandsIncrementalGenerator());

        driver.RunGeneratorsAndUpdateCompilation(compilation, out outputCompilation, out diagnostics);
    }
}

// template for the code snippets
file static class Code
{
    public const string Command =
"""
using RoyalCode.SmartCommands;
using RoyalCode.SmartProblems;
using RoyalCode.SmartValidations;

namespace Tests.Scenarios.Ds;

""";
    
    public const string Interface =
"""
using RoyalCode.SmartProblems;

namespace Tests.Scenarios.Ds;

""";
    
    public const string Handler =
"""
using RoyalCode.SmartCommands;
using RoyalCode.SmartProblems;
using Tests.Scenarios.Ds;

namespace Tests.Scenarios.Ds.Internals;

""";
}