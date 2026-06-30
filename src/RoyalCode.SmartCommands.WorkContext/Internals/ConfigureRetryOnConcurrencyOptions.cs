using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using RoyalCode.SmartCommands.WorkContext.Options;

namespace RoyalCode.SmartCommands.WorkContext.Internals;

internal sealed class ConfigureRetryOnConcurrencyOptions(IServiceProvider services) : IConfigureOptions<RetryOnConcurrencyOptions>
{
    private const string SectionName = "RetryOnConcurrency";

    public void Configure(RetryOnConcurrencyOptions options)
    {
        if (services.GetService(typeof(IConfiguration)) is not IConfiguration configuration)
            return;

        configuration.GetSection(SectionName).Bind(options);
    }
}
