# Estado SDD

<!-- Puntero de reanudación. Claude lo actualiza en CADA transición (inicio/fin de tarea, cambio de fase, decisión, bloqueo).
     Editar este archivo = checkpoint (lo registra el hook). Mantenlo corto: el detalle va en specs/ y journal.md.
     fase: constitution | specify | clarify | plan | tasks | implement | verify | done
     estado_tarea: bloqueada

feature_activa: 001-migracion-aws
fase: implement
tarea_actual: T031
estado_tarea: bloqueada

## Siguiente paso

T031: tras el merge del PR #1 (https://github.com/Unal-Team-C/urban-alert-reportes/pull/1, decisión del usuario), seguir el run de Provision en main; si falla, gh run view --log-failed, corregir con commits pequeños. Luego humo + scripts/crear-reporte-prueba.sh <gateway> y T032.

## Contexto de trabajo

- Referencias: `../Microservicio usuarios` (spec 001, infra/{base,service,routes}, scripts, workflows: el más parecido), `../Microservicio geoespacial AWS`, `../FaaS multimedia/Urban-Alert-multimedia` (infra/rutas), `../Plataforma compartida AWS` (docs/integracion.md).
- Geoespacial: `POST /api/v1/geoespacial/coordinates` es AWS_IAM (Reportes firma). Multimedia: `POST /api/v1/multimedia/images` pública (AWS_PROXY 2.0).
- Bus de EventBridge creado a mano, en ningún repo (dicho por el usuario). No hay AWS CLI local para inspeccionarlo.
- Auditoria (github Unal-Team-C/urban-alert-auditoria) aún consume por MassTransit/RabbitMQ; clon de lectura en el scratchpad de la sesión.
- Hooks del arnés usan `powershell` (no instalado en este Mac): el protocolo se sigue a mano.

## Bloqueos / preguntas abiertas

- T031 espera al merge del PR #1 (usuario). El PR no tiene checks: los workflows solo corren en push a main (validación en PR fuera de alcance).
