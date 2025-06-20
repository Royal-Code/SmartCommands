using Microsoft.Extensions.DependencyInjection;
using RoyalCode.SmartCommands.Demo.Commands.Lojas;
using RoyalCode.SmartCommands.Demo.Commands.Lojas.Internals;
using RoyalCode.SmartCommands.Demo.Commands.Produtos;
using RoyalCode.SmartCommands.Demo.Commands.Produtos.Internals;

namespace RoyalCode.SmartCommands.Demo;

public static partial class ProgramExtensions
{
    public static void AddHandlersServices(this IServiceCollection services)
    {
        services.AddTransient<ICriarLojaHandler, CriarLojaHandler>();
        services.AddTransient<ICriarProdutoHandler, CriarProdutoHandler>();
        services.AddTransient<IEditarProdutoHandler, EditarProdutoHandler>();
    }
}
