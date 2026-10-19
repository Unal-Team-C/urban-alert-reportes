# Especificación: Entrega garantizada del evento de creación y resiliencia ante dependencias

- **ID**: 002-resiliencia-eventos
- **Estado**: aprobada <!-- borrador | aclarada | aprobada | implementada -->
- **Creada**: 2026-10-19
- **Entrada original**: "Hagamos 3 y 4 de momento, usa los arneses", sobre el análisis de puntos débiles de la implementación en AWS: (3) "Sin outbox transaccional: un reporte puede quedar creado en PostgreSQL sin que su evento llegue a publicarse si el proceso falla entre el guardado y el publish" (docs/funcionamiento.md §Brechas conocidas) y (4) "Llamadas síncronas a Geoespacial (timeout 3 s) y Multimedia (10 s) solo con timeout: sin reintentos ni circuit breaker". Escenarios de calidad afectados: QAS-02 (resiliencia) y QAS-04.1 (0 eventos perdidos) de delivery4-team-C.

## 1. Resumen

Dos mejoras de confiabilidad en la creación de reportes. Primero, todo reporte que queda guardado tiene su evento de creación entregado al bus de eventos tarde o temprano, aunque el bus o el propio servicio fallen justo después de guardar; el cliente ya no recibe un error por una creación que sí ocurrió. Segundo, las llamadas a Geoespacial y Multimedia toleran fallos transitorios con reintentos acotados y, cuando una dependencia está caída de forma sostenida, el servicio deja de insistir y responde de inmediato en lugar de esperar cada timeout.

## 2. Problema y contexto

Hoy la creación guarda el reporte y después publica `ReporteCreado` en un paso aparte. Si la publicación falla (bus no disponible, entrada rechazada, proceso detenido por un despliegue o escalado a la baja), el reporte queda creado sin evento: Auditoría nunca registra esa transición, en contra de QAS-04.1 ("0 eventos perdidos"), y el cliente recibe un error aunque el reporte exista, lo que invita a reintentar y crear un duplicado.

Las llamadas a Geoespacial y Multimedia son síncronas dentro de la creación y solo tienen un timeout. Un fallo de red momentáneo hace fallar la creación completa, y si una dependencia está caída cada petición espera el timeout entero (3 s o 10 s), ocupando recursos del servicio y degradando la experiencia. QAS-02 pide detección de fallas, reintentos con espera creciente y aleatoria, y apertura de circuito con fallo rápido.

## 3. Usuarios y actores

- **Ciudadano (cliente del portal)**: que su reporte se cree aunque haya fallos momentáneos, y una respuesta rápida y clara cuando no se puede.
- **Auditor**: que toda creación de reporte quede en el historial de auditoría.
- **Equipo de operación**: saber cuándo hay eventos pendientes de entrega o una dependencia con el circuito abierto.

## 4. Historias de usuario

### HU-1: Ningún reporte creado se queda sin evento (Prioridad: P1)

Como auditor quiero que cada reporte creado genere su evento de creación en el bus para que el historial de auditoría esté completo.

**Criterios de aceptación**
1. Dado que el bus de eventos está disponible, cuando se crea un reporte, entonces su evento `ReporteCreado` llega al bus en pocos segundos (ver RNF-001).
2. Dado que el bus de eventos no está disponible, cuando se crea un reporte, entonces la creación responde éxito (201) y el evento queda pendiente; cuando el bus vuelve, el evento se entrega sin intervención manual.
3. Dado que el servicio se detiene (despliegue, escalado o caída) después de guardar un reporte y antes de entregar su evento, cuando una instancia del servicio vuelve a estar activa, entonces el evento se entrega.
4. Dado que no se pudo guardar el reporte, cuando la creación falla, entonces no existe evento pendiente ni entregado para ese reporte.

### HU-2: Fallos momentáneos de las dependencias no hacen fallar la creación (Prioridad: P1)

Como ciudadano quiero que mi reporte se cree aunque Geoespacial o Multimedia tengan un fallo de red momentáneo para no tener que repetir el formulario.

**Criterios de aceptación**
1. Dado que Geoespacial falla una vez por un error transitorio y responde bien al reintento, cuando creo un reporte, entonces el reporte se crea (201) con su coordenada.
2. Dado que Geoespacial responde que la coordenada es inválida (400), cuando creo un reporte, entonces no se reintenta y recibo el error de validación de inmediato.
3. Dado que la conexión con Multimedia no llega a establecerse una vez y sí al reintento, cuando creo un reporte con imagen, entonces el reporte se crea (201). Dado que Multimedia no responde a tiempo, entonces no se reintenta y recibo 503.

### HU-3: Fallo rápido cuando una dependencia está caída (Prioridad: P2)

Como ciudadano quiero una respuesta inmediata cuando Geoespacial o Multimedia están caídos para no esperar segundos por algo que va a fallar.

**Criterios de aceptación**
1. Dado que Geoespacial ha fallado de forma sostenida (umbral de RF-009), cuando creo un reporte, entonces recibo 503 en menos de 50 ms sin que se llame a Geoespacial.
2. Dado que el circuito está abierto, cuando pasa el tiempo de espera de RF-010, entonces se deja pasar una petición de prueba; si tiene éxito el circuito se cierra y las creaciones vuelven a funcionar.
3. Dado que el circuito de Multimedia está abierto, cuando creo un reporte **sin** imagen, entonces la creación no se ve afectada.

### Casos borde

- El bus rechaza una entrada concreta (error permanente, p. ej. formato): el evento no debe bloquear la entrega de los siguientes indefinidamente (RF-004).
- Dos instancias del servicio activas a la vez: un mismo evento pendiente no debe entregarse dos veces por la concurrencia entre instancias en condiciones normales; aun así, si ocurre una entrega duplicada, Auditoría la descarta por el identificador del evento.
- El cliente cancela la petición durante un reintento: no se siguen haciendo reintentos para esa petición.
- Reintento de Geoespacial tras un timeout en el que Geoespacial sí registró la coordenada: la repetición devuelve la misma coordenada (Geoespacial es idempotente por reporte).
- La suma de reintentos no puede llevar la creación más allá del tiempo máximo que el API Gateway espera (RNF-003).
- Se elimina un reporte cuyo evento de creación aún está pendiente: el evento se entrega igual (refleja que la creación ocurrió).

## 5. Requisitos funcionales

### Entrega del evento de creación

- **RF-001**: El sistema DEBE registrar el evento `ReporteCreado` de forma atómica con el reporte: o se guardan ambos o ninguno.
- **RF-002**: El sistema DEBE entregar al bus de eventos cada evento registrado y no entregado, sin depender de la petición que lo originó, y marcarlo como entregado al confirmarse la entrega.
- **RF-003**: El sistema DEBE reintentar la entrega de un evento no entregado con espera creciente entre intentos, hasta entregarlo.
- **RF-004**: Un evento que falla N veces seguidas (N configurable, por defecto 10) DEBE quedar marcado como fallido con el motivo del último error, dejar de reintentarse automáticamente y no impedir la entrega de los demás.
- **RF-005**: La respuesta de creación (201) DEBE darse en cuanto el reporte y su evento quedan guardados; un fallo posterior de la entrega no DEBE cambiar esa respuesta.
- **RF-006**: El contenido y el formato del evento entregado (tipo `ReporteCreado`, origen, campos y su convención de nombres y códigos) DEBEN ser idénticos a los actuales, en local (RabbitMQ) y en AWS (EventBridge).

### Llamadas a dependencias

- **RF-007**: Ante un fallo transitorio (sin conexión, timeout, o respuestas 408, 429, 500, 502, 503, 504), el sistema DEBE reintentar la llamada a Geoespacial hasta 2 veces con espera creciente y aleatoria. Para Multimedia, el sistema DEBE reintentar hasta 2 veces **solo** cuando la conexión no llegó a establecerse (la imagen nunca se envió); un timeout o una respuesta 5xx de Multimedia NO se reintentan, para no dejar copias huérfanas de la imagen (su subida no es idempotente). El circuito de RF-009 sí aplica a Multimedia.
- **RF-008**: El sistema NO DEBE reintentar respuestas que indican un error del cliente o de negocio (400, 404, 409, 413, 415).
- **RF-009**: Para cada dependencia, cuando la proporción de fallos supera el 50 % en una ventana de 30 s con al menos 5 llamadas, el sistema DEBE abrir el circuito y responder de inmediato con el mismo error de "no disponible" que hoy (503), sin llamar a la dependencia.
- **RF-010**: Con el circuito abierto, tras 15 s el sistema DEBE permitir una llamada de prueba: si tiene éxito cierra el circuito; si falla, lo mantiene abierto otros 15 s.
- **RF-011**: El estado de un circuito DEBE ser independiente por dependencia: el circuito de Multimedia no afecta a los reportes sin imagen ni a Geoespacial, y viceversa.
- **RF-012**: Los timeouts actuales por intento (Geoespacial 3 s, Multimedia 10 s) DEBEN mantenerse configurables.

### Observabilidad

- **RF-013**: El sistema DEBE registrar en los logs estructurados cada reintento (dependencia, intento, motivo), cada cambio de estado de un circuito y cada evento marcado como fallido (identificador del evento y motivo).

## 6. Requisitos no funcionales

- **RNF-001**: Con el bus disponible, el 95 % de los eventos llega al bus en menos de 5 s desde la respuesta 201.
- **RNF-002**: Con el circuito abierto, la creación responde 503 en menos de 50 ms (medido en el servicio, sin red al gateway). _(fuente: QAS-02)_
- **RNF-003**: El tiempo total de una creación, con todos los reintentos posibles, DEBE quedar por debajo de 25 s, bajo el tiempo máximo de 30 s que el API Gateway espera a la integración.
- **RNF-004**: La entrega de eventos pendientes no DEBE añadir más de 1 conexión por instancia al presupuesto de conexiones a la base (hoy acotado por instancia en la configuración del servicio).
- **RNF-005**: Garantía de entrega "al menos una vez": ningún evento de un reporte guardado se pierde (salvo los marcados como fallidos por RF-004, que quedan registrados y consultables). _(fuente: QAS-04.1)_
- **RNF-006**: Sin recursos de AWS nuevos con coste (constitución P5): la solución usa la base y el bus existentes.

## 7. Entidades clave

- **Evento pendiente**: un evento de dominio por entregar. Atributos: identificador del evento (el mismo que viaja en el evento), tipo, contenido serializado, fecha de registro, número de intentos, fecha del próximo intento, fecha de entrega (vacía si no se ha entregado), estado (pendiente, entregado, fallido) y último error.

## 8. Fuera de alcance

- Eventos para otras transiciones del reporte (verificado, asignado, etc.): no tienen contrato de evento todavía (debilidad 10 del análisis).
- Compensación de efectos en otras dependencias cuando la creación falla a medias (imagen ya subida o coordenada ya registrada y luego falla el guardado): requiere una saga; se deja documentado como brecha.
- Alarmas y notificaciones al equipo (debilidad 6): se exponen logs, pero no se crean alarmas.
- Reintento manual o reproceso de eventos marcados como fallidos: queda solo su registro.
- Purga o archivado de eventos ya entregados.
- Resiliencia de otras operaciones que no sean la creación (no llaman a dependencias).

## 9. Supuestos

- Auditoría deduplica por identificador de evento (comprobado en su almacén), así que la entrega duplicada ocasional es aceptable.
- Geoespacial es idempotente por reporte (200 ante una repetición idéntica), así que reintentarlo es seguro.
- Siempre hay al menos una instancia del servicio activa (mínimo 1 tarea), que es quien entrega los pendientes.
- Los umbrales (2 reintentos, 50 % en 30 s con mínimo 5, 15 s de espera, N = 10) son valores iniciales razonables para el prototipo y quedan configurables.
- Los eventos entregados se conservan en la base (volumen bajo en el prototipo).

## 10. Métricas de éxito

- **MS-001**: Con el bus forzado a fallar, 100 % de las creaciones responden 201 y, al restaurarlo, 100 % de sus eventos llegan al bus.
- **MS-002**: Con el proceso detenido entre el guardado y la entrega, el evento se entrega al reiniciar (0 eventos perdidos).
- **MS-003**: Con Geoespacial fallando una vez de forma transitoria, la creación tiene éxito al primer intento del cliente.
- **MS-004**: Con Geoespacial caído de forma sostenida, tras abrirse el circuito las creaciones responden 503 en menos de 50 ms.

## 11. Clarificaciones

### Sesión 2026-10-19
- P: ¿Se reintenta la subida de la imagen a Multimedia, cuya subida no es idempotente? → R: Solo cuando la conexión no llegó a establecerse; timeouts y 5xx no se reintentan; el circuito sí aplica (RF-007, HU-2 criterio 3).
- Spec aprobada por el usuario el 2026-10-19.

---

### Checklist de calidad (autorrevisión antes de entregar)

- [x] Sin detalles de implementación (lenguajes, frameworks, APIs, estructura de código)
- [x] Cada requisito es testeable y no ambiguo
- [x] Criterios de aceptación con resultado observable
- [x] Casos borde identificados
- [x] Alcance acotado (fuera de alcance explícito)
- [x] Como máximo 3 `[NEEDS CLARIFICATION]` pendientes
- [x] Coherente con `.sdd/constitution.md`
