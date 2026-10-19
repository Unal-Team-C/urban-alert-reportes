# Especificación: Verificación humana (captcha) al crear reportes

- **ID**: 003-captcha-creacion
- **Estado**: aprobada <!-- borrador | aclarada | aprobada | implementada -->
- **Creada**: 2026-10-19
- **Entrada original**: "Para el frontend quiero agregar una validación pequeña, un captcha para verificar que quien crea el reporte sea humano" + "Aplícalo", tras acordar el diseño: Cloudflare Turnstile, el token viaja en el cuerpo de la creación y lo valida el servicio de Reportes (no el API Gateway). Pareja de la feature `004-captcha-reporte` del repo `urban-alert-web`.

## 1. Resumen

La creación de reportes (`POST /api/v1/reportes`, en JSON y en multipart) exige una prueba de que la petición viene de una persona que pasó un desafío de verificación humana en el portal. El servicio comprueba esa prueba con el proveedor antes de hacer cualquier otro trabajo y rechaza la creación si falta, es inválida, ya se usó o viene de otro sitio. La verificación queda desactivada por defecto y se activa por configuración cuando existan las claves del proveedor.

## 2. Problema y contexto

La ruta de creación es pública y no tiene autenticación: cualquier script puede crear reportes en masa llamando directamente a la API, saltándose el portal. Cada creación además consume Geoespacial, Multimedia, el bus y Auditoría. Una verificación solo en el portal no sirve, porque quien llama directo a la API la evita; por eso la comprobación tiene que hacerla el servicio. Es un control antiabuso (QAS-01.1, "resistir ataques automatizados"), no autenticación: no dice quién es el usuario ni protege las rutas de modificación, que siguen abiertas (fuera de alcance).

## 3. Usuarios y actores

- **Ciudadano (portal)**: crear su reporte sin fricción adicional notable.
- **Bot o script**: no debe poder crear reportes sin superar el desafío.
- **Equipo de operación**: activar la verificación cuando tenga las claves, sin romper la creación durante el despliegue.

## 4. Historias de usuario

### HU-1: Solo se crean reportes con una prueba humana válida (Prioridad: P1)

Como equipo quiero que la creación exija una prueba de verificación humana válida para que un script no pueda crear reportes en masa.

**Criterios de aceptación**
1. Dado que la verificación está activa, cuando llega una creación con una prueba válida, de este sitio y para la acción de crear reporte, entonces el reporte se crea como hoy (201).
2. Dado que la verificación está activa, cuando llega una creación sin prueba, entonces responde 400 con un mensaje claro y no se llama a Multimedia, Geoespacial ni se guarda nada.
3. Dado que la verificación está activa, cuando la prueba es inválida, está vencida o ya se usó, entonces responde 400 y no se crea nada.
4. Dado que la verificación está activa, cuando la prueba es válida pero se resolvió en otro dominio o para otra acción, entonces responde 400.
5. Las dos formas de creación (JSON con URL de imagen y multipart con archivo) aplican la misma regla.

### HU-2: Si el proveedor no responde, no se deja pasar (Prioridad: P1)

Como equipo quiero que un fallo del proveedor de verificación no abra la puerta a creaciones sin comprobar.

**Criterios de aceptación**
1. Dado que el proveedor no responde o falla, cuando llega una creación, entonces responde 503 ("intenta más tarde") y no se crea nada.
2. Dado que el proveedor está caído de forma sostenida, entonces las creaciones fallan rápido (sin esperar el timeout en cada una).

### HU-3: Activación segura (Prioridad: P1)

Como equipo quiero activar la verificación por configuración para desplegar el código antes de tener las claves y sin romper la creación.

**Criterios de aceptación**
1. Dado que la verificación está desactivada (valor por defecto), cuando llega una creación con o sin prueba, entonces se comporta como hoy.
2. Dado que se activa, la clave secreta del proveedor nunca aparece en el repositorio, en la imagen, en los logs ni en las salidas de infraestructura.

### Casos borde

- La creación falla después de validar la prueba (p. ej. Geoespacial 503): la prueba ya quedó consumida; el portal debe pedir una nueva antes de reintentar (lo resuelve 004).
- Prueba con espacios o vacía: se trata como ausente.
- Prueba demasiado larga (más de 2048 caracteres): se rechaza sin llamar al proveedor.
- El proveedor responde éxito pero sin dominio o acción: si se configuraron los valores esperados, se rechaza.

## 5. Requisitos funcionales

- **RF-001**: Con la verificación activa, el sistema DEBE exigir en la creación (JSON y multipart) un campo con la prueba de verificación humana.
- **RF-002**: El sistema DEBE comprobar la prueba con el proveedor **antes** de validar la imagen, subirla a Multimedia, llamar a Geoespacial o guardar nada.
- **RF-003**: El sistema DEBE rechazar con 400 una prueba ausente, vacía, de más de 2048 caracteres, inválida, vencida o ya usada.
- **RF-004**: Si se configuran, el sistema DEBE rechazar con 400 las pruebas cuyo dominio no esté entre los permitidos o cuya acción no sea la esperada.
- **RF-005**: Si el proveedor no responde, responde con error o con una respuesta ilegible, el sistema DEBE responder 503 y no crear el reporte (falla cerrado).
- **RF-006**: La llamada al proveedor DEBE tener timeout, reintento solo cuando la conexión no llegó a establecerse (la prueba es de un solo uso) y circuito con fallo rápido, como las demás dependencias.
- **RF-007**: Con la verificación desactivada (por defecto), el sistema DEBE ignorar el campo y comportarse como hoy.
- **RF-008**: La activación, los dominios permitidos y la acción esperada DEBEN ser configuración; la clave secreta DEBE venir de Secrets Manager.
- **RF-009**: El sistema DEBE registrar en el log cada rechazo con el motivo devuelto por el proveedor, sin incluir la prueba ni la clave secreta.

## 6. Requisitos no funcionales

- **RNF-001**: La verificación añade como máximo el tiempo de una llamada al proveedor (timeout de 5 s por intento) al tiempo de creación; el total sigue por debajo de 25 s (spec 002 RNF-003).
- **RNF-002**: Sin coste nuevo: el proveedor es gratuito y el secreto usa Secrets Manager (≈ 0,40 USD/mes por un secreto más, documentado en la tabla de costes).
- **RNF-003**: Ninguna prueba ni secreto en logs (constitución P3).

## 7. Entidades clave

No hay datos persistidos nuevos.

## 8. Fuera de alcance

- Autenticación y protección de las rutas de modificación (`PATCH`, `PUT`, `DELETE`): debilidad 1, otra feature.
- El widget del portal: feature 004 de `urban-alert-web`.
- Crear las claves en Cloudflare: lo hace el equipo en su cuenta.
- Límite de tasa por IP.

## 9. Supuestos

- Proveedor: Cloudflare Turnstile (gratuito, casi siempre invisible). Sus claves de prueba oficiales permiten probar sin cuenta.
- La prueba viaja en el cuerpo (campo `captchaToken`) porque el CORS del gateway compartido no admite cabeceras propias.
- Los tasks de ECS tienen salida a internet (NAT de la plataforma) para llamar al proveedor.
- Orden de activación: primero se publica el portal con el widget (004) y después se activa la verificación aquí.

## 10. Métricas de éxito

- **MS-001**: Con la verificación activa, 100 % de las creaciones sin prueba válida se rechazan (400) sin efectos en otras dependencias.
- **MS-002**: Con la verificación desactivada, 0 cambios de comportamiento (las pruebas actuales siguen pasando).

## 11. Clarificaciones

<!-- /sdd-clarify añade aquí -->

---

### Checklist de calidad (autorrevisión antes de entregar)

- [x] Sin detalles de implementación (lenguajes, frameworks, APIs, estructura de código)
- [x] Cada requisito es testeable y no ambiguo
- [x] Criterios de aceptación con resultado observable
- [x] Casos borde identificados
- [x] Alcance acotado (fuera de alcance explícito)
- [x] Como máximo 3 `[NEEDS CLARIFICATION]` pendientes
- [x] Coherente con `.sdd/constitution.md`
