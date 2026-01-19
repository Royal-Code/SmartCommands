using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RoyalCode.SmartCommands.Demo.Seguranca.Domain;

namespace RoyalCode.SmartCommands.Demo.Seguranca.Infra.Data.Mappings;

/// <summary>
/// Mapping configuration for the Usuario entity.
/// </summary>
public class UsuarioMapping : IEntityTypeConfiguration<Usuario>
{
    /// <summary>
    /// Configures EF Core mappings for the Usuario entity and its complex properties.
    /// </summary>
    /// <param name="builder">EntityTypeBuilder for Usuario.</param>
    public void Configure(EntityTypeBuilder<Usuario> builder)
    {
        builder.ToTable("Usuarios");

        builder.HasKey(u => u.Id);

        builder.Property(u => u.Nome)
            .IsRequired()
            .HasMaxLength(200);

        builder.ComplexProperty(u => u.Email, email =>
        {
            email.Property(e => e.Value)
                 .IsRequired()
                 .HasMaxLength(255)
                 .HasColumnName("Email");
        });

        builder.ComplexProperty(u => u.Senha, senha =>
        {
            senha.Property(s => s.Hash)
                .IsRequired()
                .HasMaxLength(255)
                .HasColumnName("Hash");

            senha.Property(s => s.Salt)
                .IsRequired()
                .HasMaxLength(255)
                .HasColumnName("Salt");

            senha.Property(s => s.CreatedAt).IsRequired();
            senha.Property(s => s.ExpiresAt).IsRequired();
            senha.Property(s => s.Erros).IsRequired();
            senha.Property(s => s.Bloqueado).IsRequired();
        });

        builder.OwnsOne(u => u.Bloqueio, bloqueio =>
        {
            bloqueio.Property(s => s.Bloqueado).IsRequired();

            bloqueio.Property(b => b.DataBloqueio)
                     .HasColumnName("DataBloqueio");

            bloqueio.HasOne(b => b.Tipo).WithMany()
                     .HasForeignKey("TipoBloqueioId")
                     .IsRequired(false);

            bloqueio.Property(b => b.Motivo)
                     .HasMaxLength(500)
                     .HasColumnName("Motivo");
        });

        builder.Property(u => u.Ativo)
            .IsRequired();
    }
}
