using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ProFak.DB.Model;

class WalutaBuilder
{
	public static void Configure(EntityTypeBuilder<Waluta> builder)
	{
		builder.ToTable(nameof(Waluta));

		builder.HasKey(e => e.Id);

		builder.Property(e => e.Id).ValueGeneratedOnAdd().IsRequired();
		builder.Property(e => e.Skrot).HasDefaultValue("").IsRequired();
		builder.Property(e => e.Nazwa).HasDefaultValue("").IsRequired();
		builder.Property(e => e.CzyDomyslna).HasDefaultValue(false).IsRequired();
		builder.Ignore(e => e.KodISO);
		builder.Ignore(e => e.KodISOFmt);
		builder.Ignore(e => e.CzyPLN);
	}
}
