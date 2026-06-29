using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using RoyalCode.SmartCommands.Generators.Generators;
using RoyalCode.SmartCommands.WorkContext.Options;
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

        var trustedPlatformAssemblies = ((string?)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES"))
            ?.Split(Path.PathSeparator)
            ?? [];

        // assemblies references requered to compile the source code
        var referencePaths = trustedPlatformAssemblies
            .Concat(Directory.GetFiles(AppContext.BaseDirectory, "*.dll"))
            .Concat(
            [
                typeof(Util).Assembly.Location,
                typeof(object).Assembly.Location,
                typeof(CommandHandlerGenerator).Assembly.Location,
                typeof(Enumerable).Assembly.Location,
                typeof(ICollection<>).Assembly.Location,
                typeof(CommandAttribute).Assembly.Location,
                typeof(RetryOnConcurrencyOptions).Assembly.Location,
                typeof(Microsoft.Extensions.Options.IOptions<>).Assembly.Location,
                typeof(RoyalCode.WorkContext.IWorkContext).Assembly.Location,
                typeof(RoyalCode.SmartProblems.Result).Assembly.Location,
                typeof(Task).Assembly.Location,
                typeof(CancellationToken).Assembly.Location,
                typeof(Produto).Assembly.Location
            ])
            .Where(File.Exists)
            .Distinct(StringComparer.OrdinalIgnoreCase);

        var references = referencePaths.Select(path => MetadataReference.CreateFromFile(path));

        // create a compilation for the source code.
        var compilation = CSharpCompilation.Create("SourceGeneratorTests", [syntaxTree], references,
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        // apply the source generator and collect the output
        var driver = CSharpGeneratorDriver.Create(new IncrementalGenerator());

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
