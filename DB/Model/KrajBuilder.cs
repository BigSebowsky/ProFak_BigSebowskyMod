using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ProFak.DB.Model;

class KrajBuilder
{
	public static void Configure(EntityTypeBuilder<Kraj> builder)
	{
		builder.ToTable(nameof(Kraj));

		builder.HasKey(e => e.Id);

		builder.Property(e => e.Id).ValueGeneratedOnAdd().IsRequired();
		builder.Property(e => e.KodISO2).HasDefaultValue("").IsRequired();
		builder.Property(e => e.Nazwa).HasDefaultValue("").IsRequired();
		builder.Property(e => e.CzyUE).HasDefaultValue(false).IsRequired();

		builder.Ignore(e => e.CzyUEFmt);
		builder.Ignore(e => e.NazwaFmt);

		builder.HasIndex(e => e.KodISO2).IsUnique();
	}
}
