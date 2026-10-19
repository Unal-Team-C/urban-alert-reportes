# Estado SDD

<!-- Puntero de reanudación. Claude lo actualiza en CADA transición (inicio/fin de tarea, cambio de fase, decisión, bloqueo).
     Editar este archivo = checkpoint (lo registra el hook). Mantenlo corto: el detalle va en specs/ y journal.md.
     fase: constitution | specify | clarify | plan | tasks | implement | verify | done
     estado_tarea: - -->

feature_activa: 003-captcha-creacion
fase: implement
tarea_actual: T006
estado_tarea: -

## Siguiente paso

T006: comprobación completa (base nueva + --migrar + IT, unitarias Release, Terraform de los tres stacks, diff sin .github).

## Contexto de trabajo

- 002-resiliencia-eventos: cerrada (verify ✅), PR #7 abierto sin merge; 003 se apila encima.
- Token Turnstile: opaco, un solo uso, 300 s; siteverify POST https://challenges.cloudflare.com/turnstile/v0/siteverify (secret, response) → success, hostname, action, error-codes. Claves de prueba: secreto 1x0000000000000000000000000000000AA (pasa), 2x…AA (falla); token de prueba XXXX.DUMMY.TOKEN.XXXX.
- CORS del gateway solo admite content-type/authorization/x-amz-*: el token va en el cuerpo, no en un header.
- Terraform local: `.terraform` de infra/* tiene un symlink que rompe init; usar `-e TF_DATA_DIR=/tmp/tfdata --entrypoint sh` en el contenedor.
- IT local: red Podman `sdd003` (it.sh ya apunta a ella), contenedores sdd002-pg/sdd002-rmq.
- Claves reales: las crea el usuario en el panel de Cloudflare (no automatizable aquí).

## Bloqueos / preguntas abiertas

- (ninguno)
