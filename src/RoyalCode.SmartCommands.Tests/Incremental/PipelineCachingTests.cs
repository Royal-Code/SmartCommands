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

    [Fact]
    public void Changing_result_status_invalidates_command_model_and_generated_source()
    {
        const string originalSource =
            """
            using RoyalCode.SmartCommands;
            using RoyalCode.SmartProblems;

            namespace Tests.Caching;

            [MapDelete("/things/{id:int}", "delete")]
            [WithResultStatus(HttpResultStatus.Ok)]
            public class DeleteThing
            {
                [Command]
                public Result Execute([WithParameter] int id) => Result.Ok();
            }

            [MapApiHandlers]
            public static partial class Endpoints { }
            """;
        var changedSource = originalSource.Replace("HttpResultStatus.Ok", "HttpResultStatus.NoContent");
        var original = Util.CreateCompilation(originalSource);
        var driver = Util.CreateTrackedDriver().RunGenerators(original);
        var originalGenerated = GeneratedSources(driver.GetRunResult());
        var changed = original.ReplaceSyntaxTree(original.SyntaxTrees.Single(), Util.ParseSource(changedSource));

        driver = driver.RunGenerators(changed);
        var result = driver.GetRunResult();

        AssertReasons(result, IncrementalGenerator.TrackingNames.Commands, IncrementalStepRunReason.Modified);
        Assert.False(originalGenerated.SequenceEqual(GeneratedSources(result)),
            "a mudança do status deve alterar a fonte gerada");
    }

    [Fact]
    public void Changing_accepted_location_invalidates_command_model_and_generated_source()
    {
        const string originalSource =
            """
            using RoyalCode.SmartCommands;
            using RoyalCode.SmartProblems;

            namespace Tests.Caching;

            public sealed class Ticket
            {
                public int Id { get; set; }
            }

            [MapPost("/jobs", "queue-job")]
            [MapAcceptedRoute("status/{id}", nameof(Ticket.Id))]
            public class QueueJob
            {
                [Command]
                public Result<Ticket> Execute() => new Ticket { Id = 7 };
            }

            [MapApiHandlers]
            public static partial class Endpoints { }
            """;
        var changedSource = originalSource.Replace("status/{id}", "tracking/{id}", StringComparison.Ordinal);
        var original = Util.CreateCompilation(originalSource);
        var driver = Util.CreateTrackedDriver().RunGenerators(original);
        var originalGenerated = GeneratedSources(driver.GetRunResult());
        var changed = original.ReplaceSyntaxTree(original.SyntaxTrees.Single(), Util.ParseSource(changedSource));

        driver = driver.RunGenerators(changed);
        var result = driver.GetRunResult();

        AssertReasons(result, IncrementalGenerator.TrackingNames.Commands, IncrementalStepRunReason.Modified);
        Assert.False(originalGenerated.SequenceEqual(GeneratedSources(result)),
            "changing the Accepted Location must change the generated source");
    }

    [Fact]
    public void Changing_tags_invalidates_find_model_and_generated_source()
    {
        const string originalSource =
            """
            using RoyalCode.SmartCommands;

            namespace Tests.Caching;

            public class Entity { }

            [MapFind("/{id}", "find"), EntityReference<Entity, int>]
            [WithTags("Produtos")]
            public class Details { }

            [MapApiHandlers]
            public static partial class Endpoints { }
            """;
        var changedSource = originalSource.Replace("Produtos", "Catalogo");
        var original = Util.CreateCompilation(originalSource);
        var driver = Util.CreateTrackedDriver().RunGenerators(original);
        var originalGenerated = GeneratedSources(driver.GetRunResult());
        var changed = original.ReplaceSyntaxTree(original.SyntaxTrees.Single(), Util.ParseSource(changedSource));

        driver = driver.RunGenerators(changed);
        var result = driver.GetRunResult();

        AssertReasons(result, IncrementalGenerator.TrackingNames.Finds, IncrementalStepRunReason.Modified);
        Assert.False(originalGenerated.SequenceEqual(GeneratedSources(result)),
            "a mudança das tags deve alterar a fonte gerada");
    }

    [Fact]
    public void Changing_endpoint_filter_order_invalidates_search_model_and_generated_source()
    {
        const string originalSource =
            """
            using RoyalCode.SmartCommands;

            namespace Tests.Caching;

            public class Entity { }

            public sealed class FirstFilter : Microsoft.AspNetCore.Http.IEndpointFilter
            {
                public System.Threading.Tasks.ValueTask<object?> InvokeAsync(
                    Microsoft.AspNetCore.Http.EndpointFilterInvocationContext context,
                    Microsoft.AspNetCore.Http.EndpointFilterDelegate next) => next(context);
            }

            public sealed class SecondFilter : Microsoft.AspNetCore.Http.IEndpointFilter
            {
                public System.Threading.Tasks.ValueTask<object?> InvokeAsync(
                    Microsoft.AspNetCore.Http.EndpointFilterInvocationContext context,
                    Microsoft.AspNetCore.Http.EndpointFilterDelegate next) => next(context);
            }

            [MapSearch("/", "search"), SearchReference<Entity>]
            [WithEndpointFilter<FirstFilter>]
            [WithEndpointFilter<SecondFilter>]
            public class Filter { }

            [MapApiHandlers]
            public static partial class Endpoints { }
            """;
        var changedSource = originalSource
            .Replace(
                "[WithEndpointFilter<FirstFilter>]",
                "[WithEndpointFilter<TemporaryFilter>]",
                StringComparison.Ordinal)
            .Replace(
                "[WithEndpointFilter<SecondFilter>]",
                "[WithEndpointFilter<FirstFilter>]",
                StringComparison.Ordinal)
            .Replace(
                "[WithEndpointFilter<TemporaryFilter>]",
                "[WithEndpointFilter<SecondFilter>]",
                StringComparison.Ordinal);

        var original = Util.CreateCompilation(originalSource);
        var driver = Util.CreateTrackedDriver().RunGenerators(original);
        var originalGenerated = GeneratedSources(driver.GetRunResult());
        var changed = original.ReplaceSyntaxTree(original.SyntaxTrees.Single(), Util.ParseSource(changedSource));

        driver = driver.RunGenerators(changed);
        var result = driver.GetRunResult();

        AssertReasons(result, IncrementalGenerator.TrackingNames.Searches, IncrementalStepRunReason.Modified);
        Assert.False(originalGenerated.SequenceEqual(GeneratedSources(result)),
            "a mudança da ordem dos filtros deve alterar a fonte gerada");
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
