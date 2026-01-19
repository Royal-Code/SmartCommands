using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RoyalCode.SmartCommands.Demo.Seguranca.Domain;

namespace RoyalCode.SmartCommands.Demo.Seguranca.Infra.Data.Mappings;

public class PerfilMapping : IEntityTypeConfiguration<Perfil>
{
    public void Configure(EntityTypeBuilder<Perfil> builder)
    {
        builder.ToTable("Perfis");
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Nome)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(p => p.Ativo)
            .IsRequired();

        builder.OwnsMany(p => p.Permissoes, a =>
        {
            a.ToTable("PerfilPermissoes");
            a.WithOwner().HasForeignKey("PerfilId");
            a.Property<Guid>("PermissaoId")
                .HasColumnName("PermissaoId")
                .IsRequired();
            a.HasKey("PerfilId", "PermissaoId");
        });
    }
}