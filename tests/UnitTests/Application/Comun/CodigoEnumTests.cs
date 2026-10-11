using Application.Comun;
using Domain.Reportes;

namespace UnitTests.Application.Comun;

public class CodigoEnumTests
{
    [Theory]
    [InlineData(EstadoReporte.EnIntervencion, "EN_INTERVENCION")]
    [InlineData(NivelEmergencia.Alta, "ALTA")]
    [InlineData(CategoriaReporte.ViasYAndenes, "VIAS_Y_ANDENES")]
    [InlineData(CategoriaReporte.VandalismoYEdificiosPublicos, "VANDALISMO_Y_EDIFICIOS_PUBLICOS")]
    [InlineData(TipoReporte.SenalIlegibleOTapada, "SENAL_ILEGIBLE_O_TAPADA")]
    [InlineData(TipoReporte.GimnasioAlAireLibreDanado, "GIMNASIO_AL_AIRE_LIBRE_DANADO")]
    public void ACodigo_UsaUpperSnakeCase(object valor, string esperado)
    {
        string codigo = valor switch
        {
            EstadoReporte estado => CodigoEnum.ACodigo(estado),
            NivelEmergencia nivel => CodigoEnum.ACodigo(nivel),
            CategoriaReporte categoria => CodigoEnum.ACodigo(categoria),
            TipoReporte tipo => CodigoEnum.ACodigo(tipo),
            _ => throw new ArgumentOutOfRangeException(nameof(valor))
        };

        Assert.Equal(esperado, codigo);
    }

    [Fact]
    public void TodosLosValores_SeConviertenYVuelvenSinPerdida()
    {
        AssertIdaYVuelta<EstadoReporte>();
        AssertIdaYVuelta<NivelEmergencia>();
        AssertIdaYVuelta<CategoriaReporte>();
        AssertIdaYVuelta<TipoReporte>();
    }

    [Theory]
    [InlineData("EnIntervencion")]
    [InlineData("en_intervencion")]
    [InlineData("3")]
    [InlineData("")]
    [InlineData(null)]
    public void TryParse_RechazaLoQueNoEsElCodigoExacto(string? codigo)
    {
        Assert.False(CodigoEnum.TryParse(codigo, out EstadoReporte _));
    }

    [Fact]
    public void DesdeCodigo_LanzaExcepcion_SiElCodigoNoExiste()
    {
        Assert.Throws<InvalidOperationException>(() => CodigoEnum.DesdeCodigo<TipoReporte>("BACHE"));
    }

    private static void AssertIdaYVuelta<TEnum>() where TEnum : struct, Enum
    {
        List<string> codigos = Enum.GetValues<TEnum>().Select(CodigoEnum.ACodigo).ToList();

        Assert.Equal(codigos.Count, codigos.Distinct().Count());
        foreach (TEnum valor in Enum.GetValues<TEnum>())
            Assert.Equal(valor, CodigoEnum.DesdeCodigo<TEnum>(CodigoEnum.ACodigo(valor)));
    }
}
