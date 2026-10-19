# Estado SDD

<!-- Puntero de reanudación. Claude lo actualiza en CADA transición (inicio/fin de tarea, cambio de fase, decisión, bloqueo).
     Editar este archivo = checkpoint (lo registra el hook). Mantenlo corto: el detalle va en specs/ y journal.md.
     fase: constitution | specify | clarify | plan | tasks | implement | verify | done
     estado_tarea: - -->

feature_activa: 003-captcha-creacion
fase: implement
tarea_actual: -
estado_tarea: -

## Siguiente paso

Redactar specs/003-captcha-creacion/spec.md: validar en el servidor el token de Cloudflare Turnstile en POST /api/v1/reportes (JSON y multipart), desactivado por defecto; pareja de la feature 004-captcha-reporte del frontend. Rama 003-captcha-creacion sobre 002-resiliencia-eventos (PR #7 sin merge).

## Contexto de trabajo

- 002-resiliencia-eventos: cerrada (verify ✅), PR #7 abierto sin merge; 003 se apila encima.
- Token Turnstile: opaco, un solo uso, 300 s; siteverify POST https://challenges.cloudflare.com/turnstile/v0/siteverify (secret, response) → success, hostname, action, error-codes. Claves de prueba: secreto 1x0000000000000000000000000000000AA (pasa), 2x…AA (falla); token de prueba XXXX.DUMMY.TOKEN.XXXX.
- CORS del gateway solo admite content-type/authorization/x-amz-*: el token va en el cuerpo, no en un header.
- Claves reales: las crea el usuario en el panel de Cloudflare (no automatizable aquí).

## Bloqueos / preguntas abiertas

- (ninguno)
