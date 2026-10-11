using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Persistencia.Migraciones
{
    /// <inheritdoc />
    public partial class CodigosEnumEnMayusculas : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Los enums pasan de guardarse con el nombre de C# (PascalCase) al código
            // UPPER_SNAKE_CASE que expone la API (CodigoEnum). El esquema no cambia;
            // solo se reescriben los valores. El mapeo es explícito para que esta
            // migración no dependa de cómo evolucionen los enums.
            migrationBuilder.Sql("""
                UPDATE "Reportes" AS r SET "Estado" = m.nuevo
                FROM (VALUES
                ('Reportado', 'REPORTADO'),
                ('Verificado', 'VERIFICADO'),
                ('Asignado', 'ASIGNADO'),
                ('EnIntervencion', 'EN_INTERVENCION'),
                ('Resuelto', 'RESUELTO'),
                ('Rechazado', 'RECHAZADO')
                ) AS m(viejo, nuevo)
                WHERE r."Estado" = m.viejo;
                """);

            migrationBuilder.Sql("""
                UPDATE "Reportes" AS r SET "NivelEmergencia" = m.nuevo
                FROM (VALUES
                ('Default', 'DEFAULT'),
                ('Baja', 'BAJA'),
                ('Media', 'MEDIA'),
                ('Alta', 'ALTA')
                ) AS m(viejo, nuevo)
                WHERE r."NivelEmergencia" = m.viejo;
                """);

            migrationBuilder.Sql("""
                UPDATE "Reportes" AS r SET "Categoria" = m.nuevo
                FROM (VALUES
                ('ViasYAndenes', 'VIAS_Y_ANDENES'),
                ('Senalizacion', 'SENALIZACION'),
                ('AlumbradoYElectricidad', 'ALUMBRADO_Y_ELECTRICIDAD'),
                ('AguaYAlcantarillado', 'AGUA_Y_ALCANTARILLADO'),
                ('ParquesYZonasVerdes', 'PARQUES_Y_ZONAS_VERDES'),
                ('MobiliarioUrbano', 'MOBILIARIO_URBANO'),
                ('Aseo', 'ASEO'),
                ('VandalismoYEdificiosPublicos', 'VANDALISMO_Y_EDIFICIOS_PUBLICOS'),
                ('Accesibilidad', 'ACCESIBILIDAD'),
                ('Otros', 'OTROS')
                ) AS m(viejo, nuevo)
                WHERE r."Categoria" = m.viejo;
                """);

            migrationBuilder.Sql("""
                UPDATE "Reportes" AS r SET "TipoDano" = m.nuevo
                FROM (VALUES
                ('HuecosEnLaVia', 'HUECOS_EN_LA_VIA'),
                ('HundimientoDelPavimento', 'HUNDIMIENTO_DEL_PAVIMENTO'),
                ('GrietasEnLaVia', 'GRIETAS_EN_LA_VIA'),
                ('AndenRoto', 'ANDEN_ROTO'),
                ('AndenLevantadoPorRaices', 'ANDEN_LEVANTADO_POR_RAICES'),
                ('CiclorrutaDanada', 'CICLORRUTA_DANADA'),
                ('PuentePeatonalEnMalEstado', 'PUENTE_PEATONAL_EN_MAL_ESTADO'),
                ('ReductorDeVelocidadDanado', 'REDUCTOR_DE_VELOCIDAD_DANADO'),
                ('SemaforoApagado', 'SEMAFORO_APAGADO'),
                ('SemaforoDanado', 'SEMAFORO_DANADO'),
                ('SenalDeTransitoCaida', 'SENAL_DE_TRANSITO_CAIDA'),
                ('SenalIlegibleOTapada', 'SENAL_ILEGIBLE_O_TAPADA'),
                ('DemarcacionVialBorrada', 'DEMARCACION_VIAL_BORRADA'),
                ('PasoDeCebraBorrado', 'PASO_DE_CEBRA_BORRADO'),
                ('LuminariaApagada', 'LUMINARIA_APAGADA'),
                ('LuminariaIntermitente', 'LUMINARIA_INTERMITENTE'),
                ('PosteCaidoOInclinado', 'POSTE_CAIDO_O_INCLINADO'),
                ('CablesSueltosOExpuestos', 'CABLES_SUELTOS_O_EXPUESTOS'),
                ('CajaElectricaAbierta', 'CAJA_ELECTRICA_ABIERTA'),
                ('TapaDeAlcantarillaFaltante', 'TAPA_DE_ALCANTARILLA_FALTANTE'),
                ('TapaDeAlcantarillaRota', 'TAPA_DE_ALCANTARILLA_ROTA'),
                ('AlcantarillaTapada', 'ALCANTARILLA_TAPADA'),
                ('SumideroObstruido', 'SUMIDERO_OBSTRUIDO'),
                ('FugaDeAgua', 'FUGA_DE_AGUA'),
                ('Encharcamiento', 'ENCHARCAMIENTO'),
                ('Inundacion', 'INUNDACION'),
                ('MalosOlores', 'MALOS_OLORES'),
                ('ArbolCaido', 'ARBOL_CAIDO'),
                ('ArbolEnRiesgoDeCaida', 'ARBOL_EN_RIESGO_DE_CAIDA'),
                ('RamasPeligrosas', 'RAMAS_PELIGROSAS'),
                ('PastoOMalezaAlta', 'PASTO_O_MALEZA_ALTA'),
                ('JuegoInfantilDanado', 'JUEGO_INFANTIL_DANADO'),
                ('GimnasioAlAireLibreDanado', 'GIMNASIO_AL_AIRE_LIBRE_DANADO'),
                ('BancaRota', 'BANCA_ROTA'),
                ('CanecaDeBasuraDanada', 'CANECA_DE_BASURA_DANADA'),
                ('CanecaFaltante', 'CANECA_FALTANTE'),
                ('ParaderoDeBusDanado', 'PARADERO_DE_BUS_DANADO'),
                ('BarandaORejaDanada', 'BARANDA_O_REJA_DANADA'),
                ('BebederoAveriado', 'BEBEDERO_AVERIADO'),
                ('BasuraAcumulada', 'BASURA_ACUMULADA'),
                ('EscombrosEnLaVia', 'ESCOMBROS_EN_LA_VIA'),
                ('AnimalMuertoEnLaVia', 'ANIMAL_MUERTO_EN_LA_VIA'),
                ('ContenedorDesbordado', 'CONTENEDOR_DESBORDADO'),
                ('Grafitis', 'GRAFITIS'),
                ('FachadaDeteriorada', 'FACHADA_DETERIORADA'),
                ('MuroOCerramientoDanado', 'MURO_O_CERRAMIENTO_DANADO'),
                ('BanoPublicoDanado', 'BANO_PUBLICO_DANADO'),
                ('RampaDanadaOFaltante', 'RAMPA_DANADA_O_FALTANTE'),
                ('ObstaculoEnElAnden', 'OBSTACULO_EN_EL_ANDEN'),
                ('GuiaTactilDanada', 'GUIA_TACTIL_DANADA'),
                ('Otros', 'OTROS')
                ) AS m(viejo, nuevo)
                WHERE r."TipoDano" = m.viejo;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                UPDATE "Reportes" AS r SET "Estado" = m.nuevo
                FROM (VALUES
                ('REPORTADO', 'Reportado'),
                ('VERIFICADO', 'Verificado'),
                ('ASIGNADO', 'Asignado'),
                ('EN_INTERVENCION', 'EnIntervencion'),
                ('RESUELTO', 'Resuelto'),
                ('RECHAZADO', 'Rechazado')
                ) AS m(viejo, nuevo)
                WHERE r."Estado" = m.viejo;
                """);

            migrationBuilder.Sql("""
                UPDATE "Reportes" AS r SET "NivelEmergencia" = m.nuevo
                FROM (VALUES
                ('DEFAULT', 'Default'),
                ('BAJA', 'Baja'),
                ('MEDIA', 'Media'),
                ('ALTA', 'Alta')
                ) AS m(viejo, nuevo)
                WHERE r."NivelEmergencia" = m.viejo;
                """);

            migrationBuilder.Sql("""
                UPDATE "Reportes" AS r SET "Categoria" = m.nuevo
                FROM (VALUES
                ('VIAS_Y_ANDENES', 'ViasYAndenes'),
                ('SENALIZACION', 'Senalizacion'),
                ('ALUMBRADO_Y_ELECTRICIDAD', 'AlumbradoYElectricidad'),
                ('AGUA_Y_ALCANTARILLADO', 'AguaYAlcantarillado'),
                ('PARQUES_Y_ZONAS_VERDES', 'ParquesYZonasVerdes'),
                ('MOBILIARIO_URBANO', 'MobiliarioUrbano'),
                ('ASEO', 'Aseo'),
                ('VANDALISMO_Y_EDIFICIOS_PUBLICOS', 'VandalismoYEdificiosPublicos'),
                ('ACCESIBILIDAD', 'Accesibilidad'),
                ('OTROS', 'Otros')
                ) AS m(viejo, nuevo)
                WHERE r."Categoria" = m.viejo;
                """);

            migrationBuilder.Sql("""
                UPDATE "Reportes" AS r SET "TipoDano" = m.nuevo
                FROM (VALUES
                ('HUECOS_EN_LA_VIA', 'HuecosEnLaVia'),
                ('HUNDIMIENTO_DEL_PAVIMENTO', 'HundimientoDelPavimento'),
                ('GRIETAS_EN_LA_VIA', 'GrietasEnLaVia'),
                ('ANDEN_ROTO', 'AndenRoto'),
                ('ANDEN_LEVANTADO_POR_RAICES', 'AndenLevantadoPorRaices'),
                ('CICLORRUTA_DANADA', 'CiclorrutaDanada'),
                ('PUENTE_PEATONAL_EN_MAL_ESTADO', 'PuentePeatonalEnMalEstado'),
                ('REDUCTOR_DE_VELOCIDAD_DANADO', 'ReductorDeVelocidadDanado'),
                ('SEMAFORO_APAGADO', 'SemaforoApagado'),
                ('SEMAFORO_DANADO', 'SemaforoDanado'),
                ('SENAL_DE_TRANSITO_CAIDA', 'SenalDeTransitoCaida'),
                ('SENAL_ILEGIBLE_O_TAPADA', 'SenalIlegibleOTapada'),
                ('DEMARCACION_VIAL_BORRADA', 'DemarcacionVialBorrada'),
                ('PASO_DE_CEBRA_BORRADO', 'PasoDeCebraBorrado'),
                ('LUMINARIA_APAGADA', 'LuminariaApagada'),
                ('LUMINARIA_INTERMITENTE', 'LuminariaIntermitente'),
                ('POSTE_CAIDO_O_INCLINADO', 'PosteCaidoOInclinado'),
                ('CABLES_SUELTOS_O_EXPUESTOS', 'CablesSueltosOExpuestos'),
                ('CAJA_ELECTRICA_ABIERTA', 'CajaElectricaAbierta'),
                ('TAPA_DE_ALCANTARILLA_FALTANTE', 'TapaDeAlcantarillaFaltante'),
                ('TAPA_DE_ALCANTARILLA_ROTA', 'TapaDeAlcantarillaRota'),
                ('ALCANTARILLA_TAPADA', 'AlcantarillaTapada'),
                ('SUMIDERO_OBSTRUIDO', 'SumideroObstruido'),
                ('FUGA_DE_AGUA', 'FugaDeAgua'),
                ('ENCHARCAMIENTO', 'Encharcamiento'),
                ('INUNDACION', 'Inundacion'),
                ('MALOS_OLORES', 'MalosOlores'),
                ('ARBOL_CAIDO', 'ArbolCaido'),
                ('ARBOL_EN_RIESGO_DE_CAIDA', 'ArbolEnRiesgoDeCaida'),
                ('RAMAS_PELIGROSAS', 'RamasPeligrosas'),
                ('PASTO_O_MALEZA_ALTA', 'PastoOMalezaAlta'),
                ('JUEGO_INFANTIL_DANADO', 'JuegoInfantilDanado'),
                ('GIMNASIO_AL_AIRE_LIBRE_DANADO', 'GimnasioAlAireLibreDanado'),
                ('BANCA_ROTA', 'BancaRota'),
                ('CANECA_DE_BASURA_DANADA', 'CanecaDeBasuraDanada'),
                ('CANECA_FALTANTE', 'CanecaFaltante'),
                ('PARADERO_DE_BUS_DANADO', 'ParaderoDeBusDanado'),
                ('BARANDA_O_REJA_DANADA', 'BarandaORejaDanada'),
                ('BEBEDERO_AVERIADO', 'BebederoAveriado'),
                ('BASURA_ACUMULADA', 'BasuraAcumulada'),
                ('ESCOMBROS_EN_LA_VIA', 'EscombrosEnLaVia'),
                ('ANIMAL_MUERTO_EN_LA_VIA', 'AnimalMuertoEnLaVia'),
                ('CONTENEDOR_DESBORDADO', 'ContenedorDesbordado'),
                ('GRAFITIS', 'Grafitis'),
                ('FACHADA_DETERIORADA', 'FachadaDeteriorada'),
                ('MURO_O_CERRAMIENTO_DANADO', 'MuroOCerramientoDanado'),
                ('BANO_PUBLICO_DANADO', 'BanoPublicoDanado'),
                ('RAMPA_DANADA_O_FALTANTE', 'RampaDanadaOFaltante'),
                ('OBSTACULO_EN_EL_ANDEN', 'ObstaculoEnElAnden'),
                ('GUIA_TACTIL_DANADA', 'GuiaTactilDanada'),
                ('OTROS', 'Otros')
                ) AS m(viejo, nuevo)
                WHERE r."TipoDano" = m.viejo;
                """);
        }
    }
}
