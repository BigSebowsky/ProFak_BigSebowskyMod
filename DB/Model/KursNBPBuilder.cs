using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ProFak.DB.Model;

class KursNBPBuilder
{
	public static void Configure(EntityTypeBuilder<KursNBP> builder)
	{
		builder.ToTable(nameof(KursNBP));

		builder.HasKey(e => e.Id);

		builder.Property(e => e.Id).ValueGeneratedOnAdd().IsRequired();
		builder.Property(e => e.WalutaId).IsRequired();
		builder.Property(e => e.Data).IsRequired();
		builder.Property(e => e.KursSredni).HasDefaultValue(0m).IsRequired();
		builder.Property(e => e.NumerTabeli).HasDefaultValue("").IsRequired();

		builder.Ignore(e => e.WalutaRef);

		builder.HasIndex(e => new { e.WalutaId, e.Data }).IsUnique();
		builder.HasOne(e => e.Waluta).WithMany().HasForeignKey(e => e.WalutaId).OnDelete(DeleteBehavior.Cascade);
	}
}
