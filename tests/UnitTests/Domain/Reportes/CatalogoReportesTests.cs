using Domain.Reportes;

namespace UnitTests.Domain.Reportes;

public class CatalogoReportesTests
{
    [Fact]
    public void Catalogo_IncluyeCadaCategoriaUnaVez()
    {
        List<CategoriaReporte> categorias = CatalogoReportes.Categorias.Select(categoria => categoria.Categoria).ToList();

        Assert.Equal(Enum.GetValues<CategoriaReporte>().Order(), categorias.Order());
    }

    [Fact]
    public void Catalogo_IncluyeCadaTipoDeReporteUnaVez()
    {
        List<TipoReporte> tipos = CatalogoReportes.Categorias.SelectMany(categoria => categoria.Tipos).Select(tipo => tipo.Tipo).ToList();

        Assert.Equal(Enum.GetValues<TipoReporte>().Order(), tipos.Order());
    }

    [Fact]
    public void Catalogo_TieneLasDiezCategoriasYCincuentaYUnTipos()
    {
        Assert.Equal(10, CatalogoReportes.Categorias.Count);
        Assert.Equal(51, CatalogoReportes.Categorias.Sum(categoria => categoria.Tipos.Count));
    }

    [Fact]
    public void Catalogo_RespetaLaConvencionDeValoresCategoriaPorCien()
    {
        foreach (CategoriaReporteCatalogo categoria in CatalogoReportes.Categorias)
        foreach (TipoReporteCatalogo tipo in categoria.Tipos)
            Assert.Equal((int)categoria.Categoria, (int)tipo.Tipo / 100);
    }

    [Theory]
    [InlineData(TipoReporte.HuecosEnLaVia, CategoriaReporte.ViasYAndenes, true)]
    [InlineData(TipoReporte.Inundacion, CategoriaReporte.AguaYAlcantarillado, true)]
    [InlineData(TipoReporte.Otros, CategoriaReporte.Otros, true)]
    [InlineData(TipoReporte.HuecosEnLaVia, CategoriaReporte.Aseo, false)]
    [InlineData((TipoReporte)99, CategoriaReporte.ViasYAndenes, false)]
    public void PerteneceA_ValidaLaCategoriaDelTipo(TipoReporte tipo, CategoriaReporte categoria, bool esperado)
    {
        Assert.Equal(esperado, CatalogoReportes.PerteneceA(tipo, categoria));
    }

    [Fact]
    public void Nombre_DevuelveElTextoQueVeElUsuario()
    {
        Assert.Equal("Mobiliario urbano", CatalogoReportes.Nombre(CategoriaReporte.MobiliarioUrbano));
        Assert.Equal("Banca rota", CatalogoReportes.Nombre(TipoReporte.BancaRota));
    }
}
