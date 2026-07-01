namespace RoyalCode.SmartCommands.Demo.Domain;

public class ProdutoEstoque
{
	public ProdutoEstoque(Produto produto, int quantidadeInicial)
	{
		ArgumentNullException.ThrowIfNull(produto);
		ValidarQuantidadePositiva(quantidadeInicial);

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

	public void AdicionarEntrada(int quantidade)
	{
		ValidarQuantidadePositiva(quantidade);

		Disponivel += quantidade;
		Touch();
	}

	public bool TemDisponivelParaReservar(int quantidade)
	{
		ValidarQuantidadePositiva(quantidade);

		return Disponivel >= quantidade;
	}

	public void Reservar(int quantidade)
	{
		ValidarQuantidadePositiva(quantidade);

		if (Disponivel < quantidade)
			throw new InvalidOperationException("Nao ha estoque disponivel para a reserva.");

		Disponivel -= quantidade;
		Reservado += quantidade;
		Touch();
	}

	public void LiberarReserva(int quantidade)
	{
		ValidarQuantidadePositiva(quantidade);

		if (Reservado < quantidade)
			throw new InvalidOperationException("Nao ha reserva suficiente para liberar.");

		Reservado -= quantidade;
		Disponivel += quantidade;
		Touch();
	}

	private void Touch() => Version++;

	private static void ValidarQuantidadePositiva(int quantidade)
	{
		if (quantidade <= 0)
			throw new ArgumentOutOfRangeException(nameof(quantidade), quantidade, "A quantidade deve ser maior que zero.");
	}
}
