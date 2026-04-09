using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ProFak.DB.Model;

class KSeFZakupInboxBuilder
{
	public static void Configure(EntityTypeBuilder<KSeFZakupInbox> builder)
	{
		builder.ToTable(nameof(KSeFZakupInbox));

		builder.HasKey(e => e.Id);

		builder.Property(e => e.Id).ValueGeneratedOnAdd().IsRequired();
		builder.Property(e => e.NumerKSeF).HasDefaultValue("").IsRequired();
		builder.Property(e => e.Numer).HasDefaultValue("").IsRequired();
		builder.Property(e => e.DataWystawienia).IsRequired();
		builder.Property(e => e.DataSprzedazy).IsRequired();
		builder.Property(e => e.DataKSeF);
		builder.Property(e => e.DataPobrania).IsRequired();
		builder.Property(e => e.DataWeryfikacji);
		builder.Property(e => e.DataDodaniaJakoZakup);
		builder.Property(e => e.DataRozliczenia);
		builder.Property(e => e.NazwaSprzedawcy).HasDefaultValue("").IsRequired();
		builder.Property(e => e.NIPSprzedawcy).HasDefaultValue("").IsRequired();
		builder.Property(e => e.NazwaNabywcy).HasDefaultValue("").IsRequired();
		builder.Property(e => e.NIPNabywcy).HasDefaultValue("").IsRequired();
		builder.Property(e => e.RazemNetto).HasDefaultValue(0m).IsRequired();
		builder.Property(e => e.RazemVat).HasDefaultValue(0m).IsRequired();
		builder.Property(e => e.RazemBrutto).HasDefaultValue(0m).IsRequired();
		builder.Property(e => e.Waluta).HasDefaultValue("").IsRequired();
		builder.Property(e => e.TypDokumentu).HasDefaultValue("").IsRequired();
		builder.Property(e => e.XMLKSeF).HasDefaultValue("").IsRequired();
		builder.Property(e => e.URLKSeF).HasDefaultValue("").IsRequired();
		builder.Property(e => e.CzyNowa).HasDefaultValue(true).IsRequired();
		builder.Property(e => e.Status).HasDefaultValue(KSeFZakupInboxStatus.Pobrana).IsRequired();
		builder.Property(e => e.FakturaId);

		builder.Ignore(e => e.FakturaRef);
		builder.Ignore(e => e.StatusFmt);
		builder.Ignore(e => e.NumerZakupu);
		builder.Ignore(e => e.CzyDodanaJakoZakup);
		builder.Ignore(e => e.CzyRozliczona);

		builder.HasIndex(e => e.NumerKSeF).IsUnique();
		builder.HasOne(e => e.Faktura).WithMany().HasForeignKey(e => e.FakturaId).OnDelete(DeleteBehavior.SetNull);
	}
}
