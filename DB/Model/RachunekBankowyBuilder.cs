using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ProFak.DB.Model;

class RachunekBankowyBuilder
{
	public static void Configure(EntityTypeBuilder<RachunekBankowy> builder)
	{
		builder.ToTable(nameof(RachunekBankowy));

		builder.HasKey(e => e.Id);

		builder.Property(e => e.Id).ValueGeneratedOnAdd().IsRequired();
		builder.Property(e => e.KontrahentId).IsRequired();
		builder.Property(e => e.Nazwa).HasDefaultValue("").IsRequired();
		builder.Property(e => e.NumerRachunku).HasDefaultValue("").IsRequired();
		builder.Property(e => e.NazwaBanku).HasDefaultValue("").IsRequired();
		builder.Property(e => e.Swift).HasDefaultValue("").IsRequired();
		builder.Property(e => e.WalutaId);
		builder.Property(e => e.CzyDomyslny).HasDefaultValue(false).IsRequired();

		builder.Ignore(e => e.KontrahentRef);
		builder.Ignore(e => e.WalutaRef);
		builder.Ignore(e => e.NazwaFmt);
		builder.Ignore(e => e.WalutaSkrot);

		builder.HasOne(e => e.Kontrahent).WithMany(e => e.RachunkiBankowe).HasForeignKey(e => e.KontrahentId).OnDelete(DeleteBehavior.Cascade);
		builder.HasOne(e => e.Waluta).WithMany().HasForeignKey(e => e.WalutaId).OnDelete(DeleteBehavior.SetNull);
	}
}
