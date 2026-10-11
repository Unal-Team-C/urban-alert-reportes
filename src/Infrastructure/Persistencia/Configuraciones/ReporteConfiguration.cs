using Domain.Reportes;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Persistencia.Configuraciones;

public class ReporteConfiguration : IEntityTypeConfiguration<Reporte>
{
    public void Configure(EntityTypeBuilder<Reporte> builder)
    {
        builder.ToTable("Reportes");

        builder.HasKey(reporte => reporte.Id);
        builder.Property(reporte => reporte.Id).ValueGeneratedNever();

        builder.Property(reporte => reporte.Categoria)
            .HasConversion(new CodigoEnumConverter<CategoriaReporte>())
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(reporte => reporte.Tipo)
            .HasConversion(new CodigoEnumConverter<TipoReporte>())
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(reporte => reporte.Descripcion)
            .IsRequired()
            .HasMaxLength(Reporte.DescripcionMaxLength);

        builder.Property(reporte => reporte.IdCoordenada)
            .IsRequired();

        builder.Property(reporte => reporte.UrlImagen)
            .HasMaxLength(Reporte.UrlImagenMaxLength);

        builder.Property(reporte => reporte.NombreImagen)
            .HasMaxLength(Reporte.NombreImagenMaxLength);

        builder.Property(reporte => reporte.IdUsuario)
            .IsRequired();

        builder.Property(reporte => reporte.NivelEmergencia)
            .HasConversion(new CodigoEnumConverter<NivelEmergencia>())
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(reporte => reporte.Estado)
            .HasConversion(new CodigoEnumConverter<EstadoReporte>())
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(reporte => reporte.Fecha)
            .IsRequired();

        builder.Property(reporte => reporte.IdResponsable);

        builder.Property(reporte => reporte.MotivoRechazo)
            .HasMaxLength(Reporte.MotivoRechazoMaxLength);

        // "xmin" es la columna de sistema de Postgres que cambia en cada UPDATE;
        // usarla como token de concurrencia no requiere migración ni columna propia.
        builder.Property<uint>("xmin").IsRowVersion();
    }
}
