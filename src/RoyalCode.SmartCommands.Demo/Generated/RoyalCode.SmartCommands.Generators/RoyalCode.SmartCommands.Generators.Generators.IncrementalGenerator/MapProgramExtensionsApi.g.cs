using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace RoyalCode.SmartCommands.Demo;

public static partial class MapProgramExtensionsApi
{
    public static RouteGroupBuilder MapProgramExtensionsGroup(this IEndpointRouteBuilder builder)
    {
        var group = builder.MapGroup("ProgramExtensions");

        return group;
    }
}
