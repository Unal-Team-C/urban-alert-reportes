# Estado SDD

<!-- Puntero de reanudación. Claude lo actualiza en CADA transición (inicio/fin de tarea, cambio de fase, decisión, bloqueo).
     Editar este archivo = checkpoint (lo registra el hook). Mantenlo corto: el detalle va en specs/ y journal.md.
     fase: constitution | specify | clarify | plan | tasks | implement | verify | done
     estado_tarea: bloqueada

feature_activa: 001-migracion-aws
fase: implement
tarea_actual: T030
estado_tarea: bloqueada

## Siguiente paso

T030: el usuario crea/adjunta la política github-actions-urban-alert-reportes (infra/bootstrap/README.md §2) y configura AWS_ROLE_ARN + AWS_REGION en Unal-Team-C/urban-alert-reportes; con su permiso: git push -u origin aws-provisioning y gh pr create a main. Después T031 (seguir el primer Provision tras el merge) y T032.

## Contexto de trabajo

- Referencias: `../Microservicio usuarios` (spec 001, infra/{base,service,routes}, scripts, workflows: el más parecido), `../Microservicio geoespacial AWS`, `../FaaS multimedia/Urban-Alert-multimedia` (infra/rutas), `../Plataforma compartida AWS` (docs/integracion.md).
- Geoespacial: `POST /api/v1/geoespacial/coordinates` es AWS_IAM (Reportes firma). Multimedia: `POST /api/v1/multimedia/images` pública (AWS_PROXY 2.0).
- Bus de EventBridge creado a mano, en ningún repo (dicho por el usuario). No hay AWS CLI local para inspeccionarlo.
- Auditoria (github Unal-Team-C/urban-alert-auditoria) aún consume por MassTransit/RabbitMQ; clon de lectura en el scratchpad de la sesión.
- Hooks del arnés usan `powershell` (no instalado en este Mac): el protocolo se sigue a mano.

## Bloqueos / preguntas abiertas

- T030 espera al usuario: política D-016, secretos/variables de GitHub y permiso para publicar la rama y abrir el PR.
