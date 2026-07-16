using Microsoft.CodeAnalysis;
using RoyalCode.SmartCommands.Generators.Generators;
using Xunit;

namespace RoyalCode.SmartCommands.Tests.Incremental;

public class PipelineCachingTests
{
    private const string CommandSource =
        """
        using RoyalCode.SmartCommands;
        using RoyalCode.SmartProblems;

        namespace Tests.Caching;

        public class CreateThing
        {
            [Command]
            public Result Execute() => Result.Ok();
        }
        """;

    [Fact]
    public void Same_compilation_reuses_command_model()
    {
        var compilation = Util.CreateCompilation(CommandSource);
        var driver = Util.CreateTrackedDriver().RunGenerators(compilation);

        driver = driver.RunGenerators(compilation);
        var outputs = Outputs(driver.GetRunResult(), IncrementalGenerator.TrackingNames.Commands);

        Assert.NotEmpty(outputs);
        Assert.All(outputs, output => Assert.Equal(IncrementalStepRunReason.Cached, output.Reason));
    }

    [Fact]
    public void Editing_unrelated_tree_keeps_command_model_cached_or_unchanged()
    {
        var compilation = Util.CreateCompilation(CommandSource, "namespace Tests; public class Unrelated { }");
        var driver = Util.CreateTrackedDriver().RunGenerators(compilation);
        var originalSources = GeneratedSources(driver.GetRunResult());
        var unrelatedTree = compilation.SyntaxTrees.Last();
        var changedTree = Util.ParseSource("namespace Tests; public class Unrelated { public int Value { get; set; } }");
        var changedCompilation = compilation.ReplaceSyntaxTree(unrelatedTree, changedTree);

        driver = driver.RunGenerators(changedCompilation);
        var result = driver.GetRunResult();
        var outputs = Outputs(result, IncrementalGenerator.TrackingNames.Commands);

        Assert.NotEmpty(outputs);
        Assert.All(outputs, output =>
            Assert.Contains(output.Reason, new[] { IncrementalStepRunReason.Cached, IncrementalStepRunReason.Unchanged }));
        Assert.Equal(originalSources, GeneratedSources(result));
    }

    [Fact]
    public void Changing_open_api_invalidates_host_but_not_command_model()
    {
        var original = HostSource(withOpenApi: false);
        var changed = HostSource(withOpenApi: true);

        var (_, result) = RunChangedCompilation(original, changed);

        AssertReasons(result, IncrementalGenerator.TrackingNames.MapApiHandlers, IncrementalStepRunReason.Modified);
        AssertReasons(
            result,
            IncrementalGenerator.TrackingNames.Commands,
            IncrementalStepRunReason.Cached,
            IncrementalStepRunReason.Unchanged);
    }

    [Fact]
    public void Changing_find_id_type_invalidates_find_model()
    {
        const string original =
            """
            using RoyalCode.SmartCommands;

            namespace Tests.Caching;

            public class Entity { }

            [MapFind("/{id}", "find"), EntityReference<Entity, int>]
            public class Details { }
            """;
        var changed = original.Replace("Entity, int", "Entity, long", StringComparison.Ordinal);

        var (_, result) = RunChangedCompilation(original, changed);

        AssertReasons(result, IncrementalGenerator.TrackingNames.Finds, IncrementalStepRunReason.Modified);
    }

    [Fact]
    public void Changing_authorization_policy_item_invalidates_command_model()
    {
        const string original =
            """
            using RoyalCode.SmartCommands;
            using RoyalCode.SmartProblems;

            namespace Tests.Caching;

            [MapPost("/things", "create"), WithPolicy("admin")]
            public class CreateThing
            {
                [Command]
                public Result Execute() => Result.Ok();
            }
            """;
        var changed = original.Replace("admin", "editor", StringComparison.Ordinal);

        var (_, result) = RunChangedCompilation(original, changed);

        AssertReasons(result, IncrementalGenerator.TrackingNames.Commands, IncrementalStepRunReason.Modified);
    }

    private static (GeneratorDriver Driver, GeneratorDriverRunResult Result) RunChangedCompilation(
        string originalSource,
        string changedSource)
    {
        var original = Util.CreateCompilation(originalSource);
        var driver = Util.CreateTrackedDriver().RunGenerators(original);
        var changedTree = Util.ParseSource(changedSource);
        var changed = original.ReplaceSyntaxTree(original.SyntaxTrees.Single(), changedTree);

        driver = driver.RunGenerators(changed);
        return (driver, driver.GetRunResult());
    }

    private static void AssertReasons(
        GeneratorDriverRunResult result,
        string stepName,
        params IncrementalStepRunReason[] expected)
    {
        var outputs = Outputs(result, stepName);
        Assert.NotEmpty(outputs);
        Assert.All(outputs, output => Assert.Contains(output.Reason, expected));
    }

    private static string HostSource(bool withOpenApi) =>
        $$"""
        using RoyalCode.SmartCommands;
        using RoyalCode.SmartProblems;

        namespace Tests.Caching;

        public class CreateThing
        {
            [Command]
            public Result Execute() => Result.Ok();
        }

        [MapApiHandlers{{(withOpenApi ? ", WithOpenApi" : string.Empty)}}]
        public static partial class Endpoints { }
        """;

    private static (object Value, IncrementalStepRunReason Reason)[] Outputs(
        GeneratorDriverRunResult result,
        string stepName) =>
        result.Results
            .SelectMany(generator => generator.TrackedSteps)
            .Where(step => step.Key == stepName)
            .SelectMany(step => step.Value)
            .SelectMany(run => run.Outputs)
            .ToArray();

    private static string[] GeneratedSources(GeneratorDriverRunResult result) =>
        result.Results
            .SelectMany(generator => generator.GeneratedSources)
            .OrderBy(source => source.HintName, StringComparer.Ordinal)
            .Select(source => $"{source.HintName}\n{source.SourceText}")
            .ToArray();
}
