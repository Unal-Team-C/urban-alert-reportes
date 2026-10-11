# Estado SDD

<!-- Puntero de reanudación. Claude lo actualiza en CADA transición (inicio/fin de tarea, cambio de fase, decisión, bloqueo).
     Editar este archivo = checkpoint (lo registra el hook). Mantenlo corto: el detalle va en specs/ y journal.md.
     fase: constitution | specify | clarify | plan | tasks | implement | verify | done
     estado_tarea: bloqueada

feature_activa: 001-migracion-aws
fase: implement
tarea_actual: T032
estado_tarea: bloqueada

## Siguiente paso

T032: segundo Provision sin cambios de infraestructura (lo dispara el merge de este registro, rama verify/primer-provision); esperar 0 cambios en base/routes y solo imagen/task definition/servicio por el SHA nuevo. Después /sdd-verify (crear-reporte-prueba completo cuando Multimedia vuelva).

## Contexto de trabajo

- iCloud Drive crea copias "archivo 2.ext" en el repo (conflictos de sincronización): rompen el build .NET (tipos duplicados). Ignoradas en .gitignore; las halladas se movieron al scratchpad (duplicados-icloud).
- Referencias: `../Microservicio usuarios` (spec 001, infra/{base,service,routes}, scripts, workflows: el más parecido), `../Microservicio geoespacial AWS`, `../FaaS multimedia/Urban-Alert-multimedia` (infra/rutas), `../Plataforma compartida AWS` (docs/integracion.md).
- Geoespacial: `POST /api/v1/geoespacial/coordinates` es AWS_IAM (Reportes firma). Multimedia: `POST /api/v1/multimedia/images` pública (AWS_PROXY 2.0).
- Bus de EventBridge creado a mano, en ningún repo (dicho por el usuario). No hay AWS CLI local para inspeccionarlo.
- Auditoria (github Unal-Team-C/urban-alert-auditoria) aún consume por MassTransit/RabbitMQ; clon de lectura en el scratchpad de la sesión.
- Hooks del arnés usan `powershell` (no instalado en este Mac): el protocolo se sigue a mano.

## Bloqueos / preguntas abiertas

- Multimedia responde 503 directamente por el gateway (también GET metadata): su backend (DocumentDB) no está disponible. Fuera de este repo; bloquea solo la parte de imagen de MS-002.
- T032 necesita un push a main (merge de verify/primer-provision).
