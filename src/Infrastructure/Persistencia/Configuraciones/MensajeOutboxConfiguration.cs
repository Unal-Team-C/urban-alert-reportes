using Infrastructure.Mensajeria.Outbox;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Persistencia.Configuraciones;

public class MensajeOutboxConfiguration : IEntityTypeConfiguration<MensajeOutbox>
{
    public const string Tabla = "MensajesOutbox";

    public void Configure(EntityTypeBuilder<MensajeOutbox> builder)
    {
        builder.ToTable(Tabla);

        builder.HasKey(mensaje => mensaje.Id);
        builder.Property(mensaje => mensaje.Id).ValueGeneratedNever();

        builder.Property(mensaje => mensaje.Tipo).HasMaxLength(MensajeOutbox.TipoMaxLength).IsRequired();
        builder.Property(mensaje => mensaje.Contenido).HasColumnType("jsonb").IsRequired();
        builder.Property(mensaje => mensaje.RegistradoEn).IsRequired();
        builder.Property(mensaje => mensaje.Intentos).HasDefaultValue(0).IsRequired();
        builder.Property(mensaje => mensaje.ProximoIntento).IsRequired();
        builder.Property(mensaje => mensaje.UltimoError).HasMaxLength(MensajeOutbox.UltimoErrorMaxLength);

        builder.Property(mensaje => mensaje.Estado)
            .HasConversion(new CodigoEnumConverter<EstadoMensajeOutbox>())
            .HasMaxLength(20)
            .IsRequired();

        // Solo los pendientes se consultan en cada vuelta del despachador.
        builder.HasIndex(mensaje => mensaje.ProximoIntento)
            .HasDatabaseName("IX_MensajesOutbox_Pendientes")
            .HasFilter("\"Estado\" = 'PENDIENTE'");
    }
}
