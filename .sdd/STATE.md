# Estado SDD

<!-- Puntero de reanudación. Claude lo actualiza en CADA transición (inicio/fin de tarea, cambio de fase, decisión, bloqueo).
     Editar este archivo = checkpoint (lo registra el hook). Mantenlo corto: el detalle va en specs/ y journal.md.
     fase: constitution | specify | clarify | plan | tasks | implement | verify | done
     estado_tarea: hecha

feature_activa: 001-migracion-aws
fase: implement
tarea_actual: T033
estado_tarea: hecha

## Siguiente paso

T033 en PR (fix/migracion-rds). Tras el merge (usuario): seguir Provision en main (T031); si verde, humo + scripts/crear-reporte-prueba.sh y T032.

## Contexto de trabajo

- iCloud Drive crea copias "archivo 2.ext" en el repo (conflictos de sincronización): rompen el build .NET (tipos duplicados). Ignoradas en .gitignore; las halladas se movieron al scratchpad (duplicados-icloud).
- Referencias: `../Microservicio usuarios` (spec 001, infra/{base,service,routes}, scripts, workflows: el más parecido), `../Microservicio geoespacial AWS`, `../FaaS multimedia/Urban-Alert-multimedia` (infra/rutas), `../Plataforma compartida AWS` (docs/integracion.md).
- Geoespacial: `POST /api/v1/geoespacial/coordinates` es AWS_IAM (Reportes firma). Multimedia: `POST /api/v1/multimedia/images` pública (AWS_PROXY 2.0).
- Bus de EventBridge creado a mano, en ningún repo (dicho por el usuario). No hay AWS CLI local para inspeccionarlo.
- Auditoria (github Unal-Team-C/urban-alert-auditoria) aún consume por MassTransit/RabbitMQ; clon de lectura en el scratchpad de la sesión.
- Hooks del arnés usan `powershell` (no instalado en este Mac): el protocolo se sigue a mano.

## Bloqueos / preguntas abiertas

- Logs del run 38160461687 ya borrados (usuario). PR #2 (fix/migracion-rds) pendiente de merge.
