namespace Domain.Reportes;

// Convención de valores: categoría * 100 + posición dentro de la categoría.
// La pertenencia de cada tipo a su categoría se define en CatalogoReportes.
public enum TipoReporte
{
    // Vías y andenes
    HuecosEnLaVia = 101,
    HundimientoDelPavimento = 102,
    GrietasEnLaVia = 103,
    AndenRoto = 104,
    AndenLevantadoPorRaices = 105,
    CiclorrutaDanada = 106,
    PuentePeatonalEnMalEstado = 107,
    ReductorDeVelocidadDanado = 108,

    // Señalización
    SemaforoApagado = 201,
    SemaforoDanado = 202,
    SenalDeTransitoCaida = 203,
    SenalIlegibleOTapada = 204,
    DemarcacionVialBorrada = 205,
    PasoDeCebraBorrado = 206,

    // Alumbrado y electricidad
    LuminariaApagada = 301,
    LuminariaIntermitente = 302,
    PosteCaidoOInclinado = 303,
    CablesSueltosOExpuestos = 304,
    CajaElectricaAbierta = 305,

    // Agua y alcantarillado
    TapaDeAlcantarillaFaltante = 401,
    TapaDeAlcantarillaRota = 402,
    AlcantarillaTapada = 403,
    SumideroObstruido = 404,
    FugaDeAgua = 405,
    Encharcamiento = 406,
    Inundacion = 407,
    MalosOlores = 408,

    // Parques y zonas verdes
    ArbolCaido = 501,
    ArbolEnRiesgoDeCaida = 502,
    RamasPeligrosas = 503,
    PastoOMalezaAlta = 504,
    JuegoInfantilDanado = 505,
    GimnasioAlAireLibreDanado = 506,

    // Mobiliario urbano
    BancaRota = 601,
    CanecaDeBasuraDanada = 602,
    CanecaFaltante = 603,
    ParaderoDeBusDanado = 604,
    BarandaORejaDanada = 605,
    BebederoAveriado = 606,

    // Aseo
    BasuraAcumulada = 701,
    EscombrosEnLaVia = 702,
    AnimalMuertoEnLaVia = 703,
    ContenedorDesbordado = 704,

    // Vandalismo y edificios públicos
    Grafitis = 801,
    FachadaDeteriorada = 802,
    MuroOCerramientoDanado = 803,
    BanoPublicoDanado = 804,

    // Accesibilidad
    RampaDanadaOFaltante = 901,
    ObstaculoEnElAnden = 902,
    GuiaTactilDanada = 903,

    // Otros
    Otros = 1001
}
