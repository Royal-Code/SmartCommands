using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RoyalCode.SmartCommands.Demo.Seguranca.Domain;

namespace RoyalCode.SmartCommands.Demo.Seguranca.Infra.Data.Mappings;

public class PermissaoMapping : IEntityTypeConfiguration<Permissao>
{
    public void Configure(EntityTypeBuilder<Permissao> builder)
    {
        builder.ToTable("Permissoes");
        builder.HasKey(p => p.Id);

        builder.Property(p => p.Codigo)
            .IsRequired()
            .HasMaxLength(100);
        
        builder.Property(p => p.Descricao)
            .HasMaxLength(500);
    }
}
