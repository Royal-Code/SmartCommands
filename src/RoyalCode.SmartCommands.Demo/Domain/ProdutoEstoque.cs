using RoyalCode.SmartProblems;
using System.Diagnostics.CodeAnalysis;

namespace RoyalCode.SmartCommands.Demo.Domain;

public class ProdutoEstoque
{
	private ProdutoEstoque(Produto produto, int quantidadeInicial)
	{
		Id = produto.Id;
		ProdutoId = produto.Id;
		Produto = produto;
		Disponivel = quantidadeInicial;
		Version = 1;
	}

#nullable disable
	protected ProdutoEstoque() { }
#nullable enable

	public Guid Id { get; private set; }

	public Guid ProdutoId { get; private set; }

	public Produto Produto { get; private set; }

	public int Disponivel { get; private set; }

	public int Reservado { get; private set; }

	public int Version { get; private set; }

	public static Result<ProdutoEstoque> RegistrarInicial(Produto? produto, int quantidadeInicial)
	{
		if (produto is null)
			return Problems.InvalidState(
				"O produto deve ser informado para registrar estoque.",
				typeId: "demo.estoque.produto_obrigatorio");

		if (QuantidadeInvalida(quantidadeInicial, out var problem))
			return problem;

		return new ProdutoEstoque(produto, quantidadeInicial);
	}

	public Result AdicionarEntrada(int quantidade)
	{
		if (QuantidadeInvalida(quantidade, out var problem))
			return problem;

		Disponivel += quantidade;
		Touch();

		return Result.Ok();
	}

	public Result Reservar(int quantidade)
	{
		if (QuantidadeInvalida(quantidade, out var problem))
			return problem;

		if (quantidade > Disponivel)
			return Problems.InvalidState(
				$"Estoque insuficiente. Disponivel: {Disponivel}.",
				property: nameof(quantidade),
				typeId: "demo.estoque.insuficiente");

		Disponivel -= quantidade;
		Reservado += quantidade;
		Touch();

		return Result.Ok();
	}

	public Result LiberarReserva(int quantidade)
	{
		if (QuantidadeInvalida(quantidade, out var problem))
			return problem;

		if (quantidade > Reservado)
			return Problems.InvalidState(
				$"Reserva insuficiente. Reservado: {Reservado}.",
				property: nameof(quantidade),
				typeId: "demo.estoque.reserva_insuficiente");

		Reservado -= quantidade;
		Disponivel += quantidade;
		Touch();

		return Result.Ok();
	}

	private void Touch() => Version++;

	private static bool QuantidadeInvalida(int quantidade, [NotNullWhen(true)] out Problem? problem)
	{
		if (quantidade <= 0)
		{
			problem = Problems.InvalidState(
				"A quantidade deve ser maior que zero.",
				property: nameof(quantidade),
				typeId: "demo.estoque.quantidade_invalida");

			return true;
		}

		problem = null;
		return false;
	}
}
