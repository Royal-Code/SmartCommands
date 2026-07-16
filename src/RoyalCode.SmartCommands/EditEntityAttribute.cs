using System.Diagnostics;

namespace RoyalCode.SmartCommands;

/// <summary>
/// <para>
///     Marks the command method as editing an existing entity. The generated handler loads the entity of type
///     <typeparamref name="TEntity"/> by its identifier of type <typeparamref name="TId"/> before invoking the
///     command method, returning a NotFound problem when the entity does not exist.
/// </para>
/// <para>
///     Requires <see cref="WithUnitOfWorkAttribute{T}"/>. The loaded entity must be the first parameter of the command method.
/// </para>
/// <para>
///     When the command is mapped to an endpoint, the entity id is bound from a route parameter, resolved in
///     this order: the explicit <see cref="RouteParameterName"/>; the single route parameter when the template
///     declares exactly one; a route parameter named <c>{entityParameterName}Id</c>; a route parameter with the
///     same name as the entity parameter. When the template has multiple parameters and none matches, the
///     generator emits a diagnostic and does not generate the endpoint.
/// </para>
/// <para>Examples:</para>
/// <para>
/// <code>
/// // template com uma única variável: '{id}' é usada automaticamente
/// [MapPut("/{id}", "editar-produto")]
/// public class EditarProduto
/// {
///     [Command, WithUnitOfWork{AppDbContext}, EditEntity{Produto, Guid}]
///     internal void Executar(Produto produto) { /* ... */ }
/// }
///
/// // várias variáveis: '{produtoId}' casa com o parâmetro 'produto' + sufixo Id
/// [MapPut("/{lojaId}/produtos/{produtoId}", "editar-produto-da-loja")]
/// public class EditarProdutoDaLoja
/// {
///     [Command, WithUnitOfWork{AppDbContext}, EditEntity{Produto, Guid}]
///     internal void Executar(Produto produto) { /* ... */ }
/// }
///
/// // seleção explícita quando nenhuma convenção se aplica
/// [MapPut("/{parent}/itens/{codigo}", "editar-item")]
/// public class EditarItem
/// {
///     [Command, WithUnitOfWork{AppDbContext}, EditEntity{Item, int}(RouteParameterName = "codigo")]
///     internal void Executar(Item item) { /* ... */ }
/// }
/// </code>
/// </para>
/// </summary>
/// <typeparam name="TEntity">The type of the entity being edited.</typeparam>
/// <typeparam name="TId">The type of the entity identifier.</typeparam>
[AttributeUsage(AttributeTargets.Method, Inherited = false)]
[Conditional("COMPILE_TIME_ONLY")]
public class EditEntityAttribute<TEntity, TId> : Attribute
    where TEntity : class
{
    /// <summary>
    /// <para>
    ///     The name of the route parameter that carries the entity id. Optional: when omitted, the generator
    ///     resolves the parameter by convention (single route parameter; <c>{entityParameterName}Id</c>;
    ///     <c>entityParameterName</c>).
    /// </para>
    /// </summary>
    public string? RouteParameterName { get; set; }
}