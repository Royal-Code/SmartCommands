using Microsoft.Extensions.DependencyInjection;
using RoyalCode.SmartCommands.Demo.Commands.Lojas;
using RoyalCode.SmartCommands.Demo.Commands.Lojas.Internals;
using RoyalCode.SmartCommands.Demo.Commands.Produtos;
using RoyalCode.SmartCommands.Demo.Commands.Produtos.Internals;
using RoyalCode.WorkContext;

namespace RoyalCode.SmartCommands.Demo;

public static partial class ProgramExtensions
{
    public static void AddHandlersServices<TContext>(this IServiceCollection services)
        where TContext : IWorkContext
    {
        services.AddTransient<ICriarLojaHandler, CriarLojaHandler>();
        services.AddTransient<ICriarProduto2Handler, CriarProduto2Handler>();
        services.AddTransient<IDesativarProdutoHandler, DesativarProdutoHandler<TContext>>();
        services.AddTransient<IEditarProdutoHandler, EditarProdutoHandler<TContext>>();
    }
}
