using RoyalCode.SmartProblems;
using RoyalCode.SmartValidations;
using System.Diagnostics.CodeAnalysis;

namespace RoyalCode.SmartCommands.Demo.Seguranca.Domain.ValueObjects;

/// <summary>
/// Value object que contém os dados da senha de um usuário.
/// </summary>
/// <param name="Hash">Hash da senha.</param>
/// <param name="Salt">Salt usado na geração do hash.</param>
/// <param name="CreatedAt">Data de criação da senha.</param>
/// <param name="ExpiresAt">Data de expiração da senha.</param>
/// <param name="Erros">Erros de autenticação consecutivos.</param>
/// <param name="Bloqueado">Indica se a senha está bloqueada.</param>
public readonly record struct Senha(
    string Hash,
    string Salt,
    DateTimeOffset CreatedAt,
    DateTimeOffset ExpiresAt,
    int Erros,
    bool Bloqueado) : IValidable
{
    /// <summary>
    /// Retorna o valor do hash da senha.
    /// </summary>
    public override string ToString() => Hash;

    /// <summary>
    /// Valida o hash da senha usando <see cref="RuleSet"/>.
    /// </summary>
    public bool HasProblems([NotNullWhen(true)] out Problems? problems)
    {
        return RuleSet.For<Senha>()
            .NotEmpty(Hash)
            .MinLength(Hash, 20)
            .NotEmpty(Salt)
            .NotEmpty(CreatedAt)
            .NotEmpty(ExpiresAt)
            .LessThan(CreatedAt, ExpiresAt)
            .HasProblems(out problems);
    }


    /// <summary>
    /// Verifica se a senha está expirada.
    /// </summary>
    /// <returns>
    /// Verdadeiro se a senha estiver expirada; caso contrário, falso.
    /// </returns>
    public bool EstaExpirada() => DateTimeOffset.UtcNow >= ExpiresAt;

    /// <summary>
    /// Determina se o usuário pode logar com base no estado da senha.
    /// </summary>
    /// <returns>Verdadeiro se o usuário puder logar; caso contrário, falso.</returns>
    public bool PodeLogar() => !Bloqueado && !EstaExpirada();

    /// <summary>
    /// <para>
    ///     Cria uma nova instância de <see cref="Senha"/> com o número de erros incrementado em 1.
    /// </para>
    /// <para>
    ///     Usado quando o usuário erra a senha.
    /// </para>
    /// </summary>
    /// <param name="maximoErros">O número máximo de erros permitidos antes de bloquear a senha.</param>
    /// <returns>
    ///     A nova instância de <see cref="Senha"/> com o número de erros atualizado e o estado de bloqueio ajustado.
    /// </returns>
    public Senha ErrouASenha(int maximoErros)
    {
        int novosErros = Erros + 1;
        bool bloqueado = novosErros >= maximoErros;
        return this with 
        { 
            Erros = novosErros, 
            Bloqueado = bloqueado
        };
    }

    /// <summary>
    /// <para>
    ///     Cria uma nova instância de <see cref="Senha"/> com o número de erros zerado.
    /// </para>
    /// <para>
    ///     Usado quando o usuário acerta a senha.
    /// </para>
    /// </summary>
    /// <returns>
    ///     A nova instância de <see cref="Senha"/> com o número de erros zerado e o estado de bloqueio desativado.
    /// </returns>
    public Senha AcertouASenha()
    {
        return this with 
        { 
            Erros = 0, 
            Bloqueado = false
        };
    }
}
