using Microsoft.Extensions.DependencyInjection;
using RoyalCode.SmartCommands.WorkContext;
using RoyalCode.SmartCommands.WorkContext.Extensions;
using RoyalCode.SmartCommands.WorkContext.Options;
using RoyalCode.SmartProblems;

namespace RoyalCode.SmartCommands.Tests.Components;

public class ConcurrencyRetryProblemFactoryTests
{
    [Fact]
    public void Create_Must_UseTypedDelegate_WhenRegisteredForCommandAndOperation()
    {
        var services = new ServiceCollection()
            .AddConcurrencyRetryProblem<ChangePassword>(
                "account.change_password",
                static (command, context) => Problems.InvalidState(
                    $"conflict:{command.UserId}:{context.Operation}",
                    typeId: "account.concurrency"))
            .BuildServiceProvider();

        var factory = services.GetRequiredService<IConcurrencyRetryProblemFactory>();

        var problem = factory.Create(new ChangePassword("u1"), "account.change_password");

        Assert.Equal("conflict:u1:account.change_password", problem.Detail);
        Assert.Equal("account.concurrency", problem.TypeId);
    }

    [Fact]
    public void Create_Must_UseServiceDelegate_WhenRegisteredWithServiceProvider()
    {
        var services = new ServiceCollection();
        services.AddSingleton(new MessageSource("localized conflict"));
        services.AddConcurrencyRetryProblem<ChangePassword>(
            "account.change_password",
            static (sp, command, _) =>
            {
                var source = sp.GetRequiredService<MessageSource>();
                return Problems.InvalidState($"{source.Message}:{command.UserId}");
            });

        var factory = services.BuildServiceProvider()
            .GetRequiredService<IConcurrencyRetryProblemFactory>();

        var problem = factory.Create(new ChangePassword("u1"), "account.change_password");

        Assert.Equal("localized conflict:u1", problem.Detail);
    }

    [Fact]
    public void Create_Must_UseProvider_WhenProviderIsRegistered()
    {
        var services = new ServiceCollection()
            .AddConcurrencyRetryProblemProvider<ChangePassword, ChangePasswordProblemProvider>(
                "account.change_password")
            .BuildServiceProvider();

        var factory = services.GetRequiredService<IConcurrencyRetryProblemFactory>();

        var problem = factory.Create(new ChangePassword("u1"), "account.change_password");

        Assert.Equal("provider:u1", problem.Detail);
        Assert.Equal("provider.concurrency", problem.TypeId);
    }

    [Fact]
    public void Create_Must_UseConfiguredFallback_WhenNoRegistrationExists()
    {
        var services = new ServiceCollection();
        services.AddConcurrencyRetryProblems();
        services.Configure<RetryOnConcurrencyOptions>(options =>
        {
            options.ExhaustedProblemDetail = "configured detail";
            options.ExhaustedProblemTypeId = "configured.concurrency";
        });

        var factory = services.BuildServiceProvider()
            .GetRequiredService<IConcurrencyRetryProblemFactory>();

        var problem = factory.Create(new ChangePassword("u1"), "account.change_password");

        Assert.Equal("configured detail", problem.Detail);
        Assert.Equal("configured.concurrency", problem.TypeId);
    }

    private sealed record ChangePassword(string UserId);

    private sealed record MessageSource(string Message);

    private sealed class ChangePasswordProblemProvider : IConcurrencyRetryProblemProvider<ChangePassword>
    {
        public Problem Create(ChangePassword command, ConcurrencyRetryProblemContext context)
        {
            return Problems.InvalidState($"provider:{command.UserId}", typeId: "provider.concurrency");
        }
    }
}
