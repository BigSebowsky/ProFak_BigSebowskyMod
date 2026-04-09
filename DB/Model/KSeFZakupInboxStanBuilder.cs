using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ProFak.DB.Model;

class KSeFZakupInboxStanBuilder
{
	public static void Configure(EntityTypeBuilder<KSeFZakupInboxStan> builder)
	{
		builder.ToTable(nameof(KSeFZakupInboxStan));

		builder.HasKey(e => e.Id);

		builder.Property(e => e.Id).ValueGeneratedOnAdd().IsRequired();
		builder.Property(e => e.CzyAutoSynchronizacja).HasDefaultValue(true).IsRequired();
		builder.Property(e => e.InterwalSynchronizacjiMinuty).HasDefaultValue(15).IsRequired();
		builder.Property(e => e.DataPoczatkowaSynchronizacji).HasDefaultValue(new DateTime(2026, 2, 1)).IsRequired();
		builder.Property(e => e.DataOstatniejSynchronizacji);
		builder.Property(e => e.DataNastepnejSynchronizacji);
		builder.Property(e => e.LiczbaNowychDokumentow).HasDefaultValue(0).IsRequired();
	}
}
