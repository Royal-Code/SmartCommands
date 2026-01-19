using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RoyalCode.SmartCommands.Demo.Seguranca.Domain;

namespace RoyalCode.SmartCommands.Demo.Seguranca.Infra.Data.Mappings;

public class TipoBloqueioMapping : IEntityTypeConfiguration<TipoBloqueio>
{
    public void Configure(EntityTypeBuilder<TipoBloqueio> builder)
    {
        builder.ToTable("TipoBloqueio");
        builder.HasKey(tb => tb.Id);

        builder.Property(tb => tb.Nome)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(tb => tb.Descricao)
            .HasMaxLength(250);
    }
}
