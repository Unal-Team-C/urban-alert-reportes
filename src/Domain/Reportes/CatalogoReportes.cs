namespace Domain.Reportes;

public record TipoReporteCatalogo(TipoReporte Tipo, string Nombre);

public record CategoriaReporteCatalogo(CategoriaReporte Categoria, string Nombre, IReadOnlyList<TipoReporteCatalogo> Tipos);

// Fuente única de las categorías de reporte, sus tipos y los nombres que ve el usuario.
public static class CatalogoReportes
{
    public static IReadOnlyList<CategoriaReporteCatalogo> Categorias { get; } =
    [
        new(CategoriaReporte.ViasYAndenes, "Vías y andenes",
        [
            new(TipoReporte.HuecosEnLaVia, "Huecos en la vía"),
            new(TipoReporte.HundimientoDelPavimento, "Hundimiento del pavimento"),
            new(TipoReporte.GrietasEnLaVia, "Grietas en la vía"),
            new(TipoReporte.AndenRoto, "Andén roto"),
            new(TipoReporte.AndenLevantadoPorRaices, "Andén levantado por raíces"),
            new(TipoReporte.CiclorrutaDanada, "Ciclorruta dañada"),
            new(TipoReporte.PuentePeatonalEnMalEstado, "Puente peatonal en mal estado"),
            new(TipoReporte.ReductorDeVelocidadDanado, "Reductor de velocidad dañado")
        ]),
        new(CategoriaReporte.Senalizacion, "Señalización",
        [
            new(TipoReporte.SemaforoApagado, "Semáforo apagado"),
            new(TipoReporte.SemaforoDanado, "Semáforo dañado"),
            new(TipoReporte.SenalDeTransitoCaida, "Señal de tránsito caída"),
            new(TipoReporte.SenalIlegibleOTapada, "Señal ilegible o tapada"),
            new(TipoReporte.DemarcacionVialBorrada, "Demarcación vial borrada"),
            new(TipoReporte.PasoDeCebraBorrado, "Paso de cebra borrado")
        ]),
        new(CategoriaReporte.AlumbradoYElectricidad, "Alumbrado y electricidad",
        [
            new(TipoReporte.LuminariaApagada, "Luminaria apagada"),
            new(TipoReporte.LuminariaIntermitente, "Luminaria intermitente"),
            new(TipoReporte.PosteCaidoOInclinado, "Poste caído o inclinado"),
            new(TipoReporte.CablesSueltosOExpuestos, "Cables sueltos o expuestos"),
            new(TipoReporte.CajaElectricaAbierta, "Caja eléctrica abierta")
        ]),
        new(CategoriaReporte.AguaYAlcantarillado, "Agua y alcantarillado",
        [
            new(TipoReporte.TapaDeAlcantarillaFaltante, "Tapa de alcantarilla faltante"),
            new(TipoReporte.TapaDeAlcantarillaRota, "Tapa de alcantarilla rota"),
            new(TipoReporte.AlcantarillaTapada, "Alcantarilla tapada"),
            new(TipoReporte.SumideroObstruido, "Sumidero obstruido"),
            new(TipoReporte.FugaDeAgua, "Fuga de agua"),
            new(TipoReporte.Encharcamiento, "Encharcamiento"),
            new(TipoReporte.Inundacion, "Inundación"),
            new(TipoReporte.MalosOlores, "Malos olores")
        ]),
        new(CategoriaReporte.ParquesYZonasVerdes, "Parques y zonas verdes",
        [
            new(TipoReporte.ArbolCaido, "Árbol caído"),
            new(TipoReporte.ArbolEnRiesgoDeCaida, "Árbol en riesgo de caída"),
            new(TipoReporte.RamasPeligrosas, "Ramas peligrosas"),
            new(TipoReporte.PastoOMalezaAlta, "Pasto o maleza alta"),
            new(TipoReporte.JuegoInfantilDanado, "Juego infantil dañado"),
            new(TipoReporte.GimnasioAlAireLibreDanado, "Gimnasio al aire libre dañado")
        ]),
        new(CategoriaReporte.MobiliarioUrbano, "Mobiliario urbano",
        [
            new(TipoReporte.BancaRota, "Banca rota"),
            new(TipoReporte.CanecaDeBasuraDanada, "Caneca de basura dañada"),
            new(TipoReporte.CanecaFaltante, "Caneca faltante"),
            new(TipoReporte.ParaderoDeBusDanado, "Paradero de bus dañado"),
            new(TipoReporte.BarandaORejaDanada, "Baranda o reja dañada"),
            new(TipoReporte.BebederoAveriado, "Bebedero averiado")
        ]),
        new(CategoriaReporte.Aseo, "Aseo",
        [
            new(TipoReporte.BasuraAcumulada, "Basura acumulada"),
            new(TipoReporte.EscombrosEnLaVia, "Escombros en la vía"),
            new(TipoReporte.AnimalMuertoEnLaVia, "Animal muerto en la vía"),
            new(TipoReporte.ContenedorDesbordado, "Contenedor desbordado")
        ]),
        new(CategoriaReporte.VandalismoYEdificiosPublicos, "Vandalismo y edificios públicos",
        [
            new(TipoReporte.Grafitis, "Grafitis"),
            new(TipoReporte.FachadaDeteriorada, "Fachada deteriorada"),
            new(TipoReporte.MuroOCerramientoDanado, "Muro o cerramiento dañado"),
            new(TipoReporte.BanoPublicoDanado, "Baño público dañado")
        ]),
        new(CategoriaReporte.Accesibilidad, "Accesibilidad",
        [
            new(TipoReporte.RampaDanadaOFaltante, "Rampa dañada o faltante"),
            new(TipoReporte.ObstaculoEnElAnden, "Obstáculo en el andén"),
            new(TipoReporte.GuiaTactilDanada, "Guía táctil dañada")
        ]),
        new(CategoriaReporte.Otros, "Otros",
        [
            new(TipoReporte.Otros, "Otros")
        ])
    ];

    private static readonly Dictionary<TipoReporte, CategoriaReporte> CategoriaPorTipo = Categorias
        .SelectMany(categoria => categoria.Tipos.Select(tipo => (tipo.Tipo, categoria.Categoria)))
        .ToDictionary(par => par.Tipo, par => par.Categoria);

    public static bool PerteneceA(TipoReporte tipo, CategoriaReporte categoria) =>
        CategoriaPorTipo.TryGetValue(tipo, out CategoriaReporte categoriaDelTipo) && categoriaDelTipo == categoria;

    public static string Nombre(CategoriaReporte categoria) =>
        Categorias.FirstOrDefault(c => c.Categoria == categoria)?.Nombre ?? categoria.ToString();

    public static string Nombre(TipoReporte tipo) =>
        Categorias.SelectMany(c => c.Tipos).FirstOrDefault(t => t.Tipo == tipo)?.Nombre ?? tipo.ToString();
}
