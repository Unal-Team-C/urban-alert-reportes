# Especificación: Migración del Servicio de Reportes a AWS

- **ID**: 001-migracion-aws
- **Estado**: aclarada <!-- borrador | aclarada | aprobada | implementada -->
- **Creada**: 2026-10-11
- **Entrada original**: "Ok now we must migrate the service to aws, follow the README in urban-alert/infrastructure. This service must be migrated to an ECS express mode and database must have its own postgres instance. Multimedia, geoespacial and the network itself (check the urban-alert-plataforma repo in the aws migration folder) to understand how it's deployed. There is already a deployed amazon event bridge that replaces the rabbitmq" + "No repo has the event bus as someone deployed it manually". Fuentes en `fuentes/`: README de `UrbanAlert/infrastructure` (rutas y aislamiento locales), README y guía de integración de `urban-alert-plataforma`. Referencias de práctica: `urban-alert-geoespacial` (spec 002), `urban-alert-usuarios` (spec 001), `Urban-Alert-multimedia` (spec 002).

## 1. Resumen

Llevar el Servicio de Reportes (.NET 10), que hoy solo corre en contenedores locales junto a RabbitMQ, a AWS: servicio de contenedores en modo Express con escalado automático y una base PostgreSQL gestionada **propia del servicio**, ambos en la red privada compartida de UrbanAlert y publicados **solo** a través del API Gateway compartido, con las mismas rutas que el gateway local. En la nube, Reportes llama a Geoespacial y a Multimedia por el gateway (como ya están desplegados) y publica su evento de creación en el bus de Amazon EventBridge existente en lugar de RabbitMQ. Toda la infraestructura del servicio queda como código y se aprovisiona con GitHub Actions en cada commit a `main`; otro flujo, manual, la elimina. El contrato de la API no cambia.

## 2. Problema y contexto

- El servicio solo se puede usar en la máquina de cada integrante (`docker compose` con PostgreSQL y RabbitMQ). El frontend no tiene una instancia compartida a la que enviar reportes.
- En local, el gateway Nginx publica `/api/v1/reportes/*` → `/api/v1/Reportes/*` sin autenticación, y la red `data-reportes` aísla su base (`fuentes/UrbanAlert-infrastructure-README.md`). En AWS ese papel lo cumplen el API Gateway y la red privada de la plataforma `Unal-Team-C/urban-alert-plataforma` (ya aprovisionada), con su contrato en SSM (`/urban-alert/plataforma/`) y su guía (`fuentes/plataforma-integracion.md`).
- Dependencias síncronas al crear un reporte, ya desplegadas en AWS:
  - **Geoespacial** (ECS Express): la asignación de coordenadas `POST /api/v1/geoespacial/coordinates` es una ruta **interna** del gateway que exige firma IAM; el repo de Geoespacial la registró pensando en Reportes como único llamador. Reportes debe firmar y tener permiso de invocación.
  - **Multimedia** (AWS Lambda): `POST /api/v1/multimedia/images` es **pública** en el gateway. Hoy Reportes no la llama por HTTP normal, sino construyendo a mano el evento del emulador local de Lambda, que en la nube no existe.
- Evento asíncrono: Reportes publica `ReporteCreadoEvent` por MassTransit/RabbitMQ y lo consume `urban-alert-auditoria`. En AWS ya existe un **bus de Amazon EventBridge creado a mano** (fuera de cualquier repo) que reemplaza a RabbitMQ.
- En local, Auditoria lee además la tabla `Reportes` de esta base con un rol `reportes_reader` (`infrastructure/postgres-reportes/01-auditoria.sh`) para autorizar por reporte.
- Configuración actual de desarrollo: las migraciones de EF Core solo se aplican al arrancar en `Development`, logs en texto, sin cifrado de la conexión, imagen sin comprobación de salud en el puerto documentado para la plataforma.

## 3. Usuarios y actores

- **Equipo de desarrollo**: publicar cambios del servicio y de su infraestructura con un commit a `main` y eliminar el entorno a mano cuando no se usa.
- **Frontend / ciudadanos y administradores**: crear, consultar y gestionar reportes a través de la URL del API Gateway.
- **Servicio de Auditoria**: recibir el evento de creación de cada reporte desde el bus de EventBridge.
- **Servicios Geoespacial y Multimedia** (dependencias): reciben llamadas de Reportes por el gateway.
- **Revisor / docente**: entender desde el repositorio qué infraestructura existe y cómo se despliega.

## 4. Historias de usuario

### HU-1: Aprovisionar y desplegar con un commit a `main` (Prioridad: P1)

Como integrante del equipo quiero que un commit a `main` aprovisione la infraestructura y deje desplegada la versión de ese commit, sin pasos manuales.

**Criterios de aceptación**
1. Dado un commit a `main`, cuando termina el flujo de aprovisionamiento, entonces el servicio responde con la versión de ese commit a través del API Gateway y su comprobación de salud interna es saludable.
2. Dado un commit a `main` que hace fallar las pruebas, cuando se ejecuta el flujo, entonces no se modifica la infraestructura ni se despliega nada, y el flujo queda en rojo.
3. Dado un commit sin cambios de infraestructura, cuando se ejecuta el flujo, entonces el aprovisionamiento informa de que no hay cambios.
4. Dados dos commits seguidos, cuando sus flujos coinciden, entonces se ejecutan uno detrás de otro.

### HU-2: Desaprovisionar el entorno a mano (Prioridad: P1)

Como integrante del equipo quiero lanzar a mano un flujo que elimine el entorno del servicio, para no pagar cuando no se usa y poder recrearlo con un commit.

**Criterios de aceptación**
1. Dado el repositorio en GitHub, cuando un integrante lanza el flujo y confirma escribiendo el nombre del entorno, entonces se eliminan primero las rutas del servicio en el gateway y después todos los recursos del servicio gestionados como código.
2. Dado que se lanza sin la confirmación exacta, entonces no elimina nada y lo indica.
3. Dada la base con datos, cuando se desaprovisiona, entonces antes de eliminarla queda una copia final restaurable.
4. Dado el entorno desaprovisionado, cuando se hace un commit a `main`, entonces se recrea completo y el servicio vuelve a estar saludable.
5. Desaprovisionar este servicio no toca la plataforma compartida, los recursos de arranque, el bus de EventBridge ni los recursos de otros microservicios (Geoespacial, Usuarios y Multimedia siguen respondiendo).
6. El flujo nunca se ejecuta por un commit.

### HU-3: Base PostgreSQL propia del servicio (Prioridad: P1)

Como equipo queremos que Reportes tenga su propia instancia de PostgreSQL gestionada, separada de las de otros servicios, para aislar sus datos como en local.

**Criterios de aceptación**
1. Dado el entorno aprovisionado, cuando se listan las bases de datos de la cuenta, entonces Reportes tiene una instancia propia que no comparte con ningún otro servicio.
2. Dado el entorno recién aprovisionado, cuando se consulta la base, entonces el esquema es exactamente el de las migraciones versionadas del repositorio (todas aplicadas) y no hay reportes.
3. Dado el entorno aprovisionado, cuando se intenta conectar a la base desde internet o desde tareas de otro servicio, entonces no es posible; solo llegan las tareas de Reportes.
4. Dado el servicio desplegado, cuando se conecta a la base, entonces la conexión va cifrada y usa un usuario distinto del administrador, sin permisos para alterar el esquema.

### HU-4: Crear reportes con sus dependencias en la nube (Prioridad: P1)

Como ciudadano quiero crear un reporte (con coordenada y, opcionalmente, imagen) por el gateway, y que se registre igual que en local.

**Criterios de aceptación**
1. Dado el servicio desplegado, cuando se crea un reporte con coordenada válida, entonces Reportes obtiene el id de coordenada de Geoespacial a través del gateway con una petición firmada, guarda el reporte y responde 201 con `idCoordenada`.
2. Dado un reporte enviado con archivo de imagen (`multipart/form-data`), cuando se crea, entonces Reportes sube la imagen a Multimedia a través del gateway y guarda la `urlImagen` que este devuelve; la URL abre la imagen.
3. Dado que Geoespacial o Multimedia no responden o fallan, cuando se crea un reporte, entonces el reporte no se crea y la respuesta es la misma que en local (503 / 400 según el caso).
4. Dado un reporte creado, cuando termina la creación, entonces un evento `ReporteCreado` con los mismos datos que hoy (`IdEvento`, `Id`, `Estado`, `IdCoordenada`, `IdUsuario`, `Fecha`) queda publicado en el bus de EventBridge existente. El evento va al bus `urban-alert-bus-eventos` con `source = urban-alert.reportes`, `detail-type = ReporteCreado` y como `detail` el JSON actual del evento (RF-012).
5. Dado el entorno local (`docker compose` + `dotnet run`), cuando se crea un reporte, entonces sigue funcionando con RabbitMQ, el emulador de Lambda de Multimedia y Geoespacial sin firma, como hoy.

### HU-5: Servicio apto para la nube (Prioridad: P1)

Como equipo queremos que el servicio se configure, registre su actividad y se apague como espera la plataforma de contenedores.

**Criterios de aceptación**
1. Dado el servicio desplegado, cuando recibe peticiones, entonces cada petición y cada error quedan en sus logs en la consola de AWS, una entrada estructurada por evento con nivel y marca de tiempo.
2. Dado que la plataforma detiene una tarea, cuando la tarea recibe la señal de parada, entonces termina las peticiones en curso y cierra sus conexiones antes de salir.
3. Dado un nuevo despliegue, cuando se reemplazan las tareas, entonces las rutas del servicio siguen respondiendo durante el cambio.

### HU-6: Exponer el servicio solo a través del API Gateway compartido (Prioridad: P1)

Como equipo queremos que Reportes sea accesible únicamente por el gateway de la plataforma, con las mismas rutas que el gateway local.

**Criterios de aceptación**
1. Dado el servicio desplegado, cuando un cliente llama a `<gateway>/api/v1/reportes` o a cualquier ruta bajo `<gateway>/api/v1/reportes/…` (lista, catálogo, detalle, creación JSON y multipart, estado, nivel de emergencia, asignación, rechazo, borrado), entonces recibe la misma respuesta (código y cuerpo) que daría la ruta equivalente `/api/v1/Reportes/…` del servicio. Todas las rutas son públicas, sin autenticación de clientes, como en local.
2. Dado el servicio desplegado, cuando se intenta llegar a él desde internet sin pasar por el gateway, entonces no es posible: no tiene URL pública propia ni sus tareas tienen IP pública.
3. Dada una ruta fuera de `/api/v1/reportes` (p. ej. `/api/v1/reportes-x`, `/health`, `/scalar/v1`, `/openapi/v1.json`), cuando se llama al gateway, entonces responde 404 sin llegar al servicio.
4. Dado que el gateway gestiona CORS, cuando el frontend llama desde su origen permitido, entonces el servicio no añade cabeceras CORS propias que entren en conflicto.

### HU-7: Entender y reproducir el despliegue (Prioridad: P2)

Como revisor o nuevo integrante quiero encontrar en el repositorio cómo se despliega el servicio y qué hay que configurar.

**Criterios de aceptación**
1. Dada una cuenta con solo los recursos de arranque, la plataforma, Geoespacial, Multimedia y el bus de EventBridge, cuando se siguen los pasos documentados y se hace un commit a `main`, entonces se obtiene el entorno funcionando.
2. Dado el README, cuando se busca la lista de variables y secretos de GitHub, las rutas publicadas, las dependencias externas (incluido el bus) y los costes, entonces aparecen con su propósito y un ejemplo (sin valores secretos).

### Casos borde

- El aprovisionamiento falla a mitad → el siguiente commit retoma desde el estado registrado sin duplicar recursos.
- Un recurso gestionado se cambia a mano en la consola → el siguiente aprovisionamiento lo devuelve a lo definido en el repositorio.
- Primer despliegue: la base tarda varios minutos en estar disponible → el servicio no se da por desplegado hasta que su comprobación de salud responde saludable.
- La base no está disponible tras el despliegue → la comprobación de salud falla y la plataforma no envía tráfico a esas tareas.
- El escalado llega al máximo de tareas → el total de conexiones a la base no supera el presupuesto de RNF-002.
- Se intenta borrar la base desde el flujo de aprovisionamiento → la protección contra borrado lo impide y el flujo falla.
- Se desaprovisiona con el entorno ya eliminado → termina sin error indicando que no hay nada que eliminar.
- Se lanza el desaprovisionamiento mientras corre un aprovisionamiento → espera a que termine.
- La plataforma compartida no está aprovisionada → el aprovisionamiento falla antes de cambiar nada, indicándolo.
- El bus de EventBridge no existe (lo borraron a mano) → el aprovisionamiento falla antes de cambiar nada, indicándolo.
- Ya existe en el gateway una ruta con la misma clave → el aprovisionamiento falla con conflicto y la ruta existente no cambia.
- Geoespacial o Multimedia están desaprovisionados → Reportes arranca y responde en lecturas; las creaciones que los necesitan responden 503 como en local.
- La publicación del evento falla después de guardar el reporte → mismo comportamiento que hoy con RabbitMQ (brecha conocida: sin outbox), registrado en logs.
- Una imagen supera el tamaño máximo que admite el gateway → Reportes responde con un error de validación, no 500.
- Un cambio de esquema (nueva migración) llega con varias tareas arrancando a la vez → se aplica una sola vez y ninguna tarea arranca con un esquema a medias.
- El flujo se lanza desde una rama distinta de `main` o desde un fork → no obtiene credenciales de AWS.

## 5. Requisitos funcionales

- **RF-001**: El servicio DEBE leer su configuración del entorno; en la nube, las credenciales de la base DEBEN venir del almacén de secretos de AWS y no aparecer en el repositorio, en la imagen ni en los logs.
- **RF-002**: En la nube, la conexión a la base DEBE ir cifrada; si no se puede cifrar, el servicio DEBE fallar en lugar de conectarse sin cifrar.
- **RF-003**: El tamaño del pool de conexiones DEBE cumplir `conexiones por tarea × tareas máximas ≤ presupuesto` (RNF-002), comprobado antes de desplegar.
- **RF-004**: El servicio DEBE escribir sus logs en la salida estándar, una entrada estructurada por línea (nivel, marca de tiempo, mensaje), con una entrada por petición que incluya método, ruta, código de respuesta y duración.
- **RF-005**: Al recibir la señal de parada, el servicio DEBE dejar de aceptar peticiones, terminar las que están en curso y cerrar sus conexiones antes de salir, dentro del margen de parada de la plataforma.
- **RF-006**: La imagen DEBE escuchar en un puerto fijo documentado, exponer una comprobación de salud que refleje la disponibilidad de la base, ejecutarse sin privilegios de administrador y estar construida para la arquitectura de la plataforma.
- **RF-007**: Cada imagen publicada DEBE etiquetarse con el identificador del commit, de forma inmutable.
- **RF-008**: El esquema en la nube DEBE salir únicamente de las migraciones versionadas de EF Core del repositorio, aplicarse una sola vez por versión antes de que la nueva versión reciba tráfico, y ser idempotente al repetir el despliegue. El servicio no aplica migraciones al arrancar fuera de `Development`.
- **RF-009**: El servicio DEBE conectarse con un usuario propio con permisos de lectura y escritura de datos sobre sus tablas y sin permisos de DDL, distinto del usuario administrador; las migraciones las aplica una identidad con permisos de esquema.
- **RF-010**: En la nube, Reportes DEBE llamar a Geoespacial solo por la URL del gateway (`POST /api/v1/geoespacial/coordinates`), firmando cada petición con las credenciales de su rol de tarea, que DEBE tener permiso de invocación solo sobre esa ruta. En local DEBE seguir llamando directamente, sin firma.
- **RF-011**: En la nube, Reportes DEBE subir imágenes a Multimedia por la URL del gateway (`POST /api/v1/multimedia/images`) como petición `multipart/form-data` normal, interpretando sus respuestas (201, 400/413/415, otros) igual que hoy. En local DEBE poder seguir usando el emulador de Lambda.
- **RF-012**: En la nube, Reportes DEBE publicar el evento de creación de reporte en el bus de EventBridge existente (externo, no gestionado por este repo), con permiso de su rol de tarea limitado a ese bus; en local DEBE seguir publicando por RabbitMQ. El bus es `arn:aws:events:us-east-2:348364742283:event-bus/urban-alert-bus-eventos`. Cada entrada lleva `Source = urban-alert.reportes`, `DetailType = ReporteCreado` y `Detail` = `ReporteCreadoEvent` serializado en JSON camelCase (`idEvento`, `reporte{id, estado, idCoordenada, idUsuario, fecha}`), con los enums como códigos `UPPER_SNAKE_CASE` igual que la API. Una entrada rechazada por EventBridge cuenta como fallo de publicación.
- **RF-013**: Las URL del gateway, el nombre del bus y demás datos de las dependencias DEBEN tomarse en cada aprovisionamiento de su fuente (contrato SSM de la plataforma; el bus, de la configuración documentada en el repo), sin IDs copiados a mano en el código.
- **RF-014**: Toda la infraestructura del servicio DEBE estar definida como código en este repositorio: instancia PostgreSQL propia, reglas de red de acceso a ella, secretos, registro de imágenes, roles propios de ejecución, de tarea y de infraestructura, servicio Express con escalado, comprobación de salud, logs y rutas en el gateway.
- **RF-015**: El servicio NO DEBE gestionar ni reutilizar recursos de otros microservicios (roles, clúster, base, grupos de seguridad, registro de imágenes, rutas), ni de la plataforma, ni el bus de EventBridge, ni recursos de arranque; solo los lee o usa cuando los necesita.
- **RF-016**: El estado de la infraestructura DEBE guardarse de forma remota, cifrada y con bloqueo, bajo una clave propia del servicio.
- **RF-017**: El flujo de aprovisionamiento DEBE ejecutarse en cada push a `main` y, en este orden: comprobar que la plataforma y el bus existen; ejecutar las pruebas unitarias y de integración y las comprobaciones estáticas; aprovisionar los recursos base; construir y publicar la imagen; aplicar el esquema (RF-008); desplegar la versión; registrar o actualizar las rutas en el gateway (RF-021); comprobar a través del gateway que responde. Un paso fallido detiene los siguientes.
- **RF-018**: Debe existir un flujo de desaprovisionamiento solo manual, con confirmación explícita (escribir el nombre del entorno), que elimine primero las rutas, después el servicio y después los recursos base, dejando copia final de la base. La protección contra borrado solo la levanta este flujo.
- **RF-019**: Los flujos DEBEN autenticarse con credenciales temporales (federación de identidad de GitHub), sin claves de larga duración, y DEBEN impedir ejecuciones simultáneas sobre la misma infraestructura.
- **RF-020**: El servicio, la base y cualquier tarea puntual DEBEN desplegarse en la red privada compartida, tomando VPC y subredes del contrato SSM de la plataforma en cada aprovisionamiento (todas las subredes privadas). Sin URL pública propia ni IP pública en las tareas.
- **RF-021**: El servicio DEBE registrar en el gateway compartido rutas que reproduzcan la convención local `/api/v1/reportes/*` → `/api/v1/Reportes/*` para todos los endpoints de `ReportesController` (incluida la raíz `/api/v1/reportes` para listar y crear) y ninguna otra (salud, OpenAPI, Scalar, raíz). Todas sin autenticación de clientes (`NONE`), como el gateway local.
- **RF-022**: Si la plataforma compartida o el bus no existen, el aprovisionamiento DEBE fallar antes de modificar ningún recurso, con un mensaje que lo indique.
- **RF-023**: El repositorio DEBE documentar: los recursos de arranque que usa y cómo comprobarlos; variables y secretos de GitHub; cómo funciona cada flujo; las rutas publicadas; las dependencias externas (Geoespacial, Multimedia, bus); los costes; y el inventario de recursos que crea.
- **RF-024**: El entorno local (`dotnet run` en `Development`, `docker compose` de `deploy/docker`, compose de `UrbanAlert/infrastructure`) y la suite de pruebas DEBEN seguir funcionando sin cambios en su forma de uso.

## 6. Requisitos no funcionales

- **RNF-001 (seguridad)**: Ningún secreto en el repositorio, la imagen, las variables de GitHub ni los logs; la base no es pública y solo admite conexiones de las tareas de Reportes; el servicio solo es alcanzable desde internet por el gateway; permisos del rol de tarea mínimos (una ruta de Geoespacial, un bus).
- **RNF-002 (conexiones)**: Con el escalado al máximo, las conexiones simultáneas del servicio a su base no superan 50 (la instancia más pequeña admite ~80; el resto queda para migraciones y administración).
- **RNF-003 (idempotencia)**: Dos aprovisionamientos seguidos sobre el mismo commit dan 0 cambios de infraestructura en el segundo.
- **RNF-004 (disponibilidad en despliegue)**: Durante un despliegue las rutas del servicio siguen respondiendo (sin ventana de caída).
- **RNF-005 (duración)**: Un aprovisionamiento sin cambios de base de datos termina en menos de 20 minutos.
- **RNF-006 (coste)**: Tamaños mínimos (una zona para la base, clase de instancia más pequeña adecuada, mínimo una tarea), con su coste mensual documentado.
- **RNF-007 (protección de datos)**: Copias automáticas con al menos 7 días de retención y protección contra borrado accidental.
- **RNF-008 (latencia de creación)**: Pasar por el gateway no cambia los límites de tiempo actuales hacia Geoespacial (3 s) y Multimedia (10 s); la creación completa cabe en el máximo de 30 s del gateway.

## 7. Entidades clave

Sin entidades nuevas: el modelo `Reporte` y el evento `ReporteCreadoEvent` no cambian de contenido. Recursos relevantes:

- **Entorno del servicio**: base PostgreSQL propia, secretos de conexión, registro de imágenes, roles propios, servicio de contenedores con el balanceador interno compartido por Express, rutas en el gateway, reglas de red y logs.
- **Plataforma compartida** (externa): red privada, salida a internet y API Gateway; solo se consume.
- **Dependencias externas**: rutas de Geoespacial y Multimedia en el gateway; bus de EventBridge (creado a mano). Solo se usan.

## 8. Fuera de alcance

- Varios entornos; dominio o certificado propios.
- Crear o modificar la plataforma compartida, el bus de EventBridge, sus reglas o destinos, Geoespacial, Multimedia o Auditoria (incluido adaptar el consumidor de Auditoria a EventBridge).
- Acceso de Auditoria a la base de Reportes (rol `reportes_reader` y regla de red): se añadirá cuando se migre Auditoria.
- Autenticación de clientes finales en el gateway y autorización por rol en el servicio.
- Publicar la documentación interactiva (Scalar) por el gateway (en local es `/docs/reportes`).
- Nuevos eventos (cambio de estado, asignación, etc.) y outbox transaccional (brechas conocidas de `docs/funcionamiento.md`).
- Configurar el frontend para que use la nueva URL.
- Migrar datos existentes desde entornos locales.
- RDS Proxy, multi-zona, réplicas; paneles y alarmas adicionales.
- Validación del pipeline en pull requests.

## 9. Supuestos

- Cuenta `348364742283`, región `us-east-2`; la plataforma, Geoespacial y Multimedia están aprovisionados, con las rutas de sus repos (`POST /api/v1/geoespacial/coordinates` con `AWS_IAM`; `POST /api/v1/multimedia/images` pública, payload 2.0 hacia Lambda).
- El bus de EventBridge `urban-alert-bus-eventos` está en la misma cuenta y región.
- El rol de los workflows y sus políticas ya permiten gestionar recursos con prefijo `urban-alert-*` (ECR, ECS, RDS, IAM, secretos, logs, API Gateway, SSM); si falta un permiso (p. ej. para describir el bus), se añade al JSON versionado correspondiente y lo aplica un administrador.
- Los secretos `AWS_ROLE_ARN` y la variable `AWS_REGION` se configuran en el repo `urban-alert-reportes` (la confianza OIDC ya cubre `Unal-Team-C/*` en `main`).
- El registro de imágenes del servicio no existe todavía y lo crea el código.
- Express comparte un balanceador interno por VPC (ya creado); Reportes añade su regla por cabecera `Host`.
- La base es PostgreSQL 17 (en local se usa 16; el esquema es compatible) con la contraseña del administrador gestionada por AWS.
- Escalado por defecto de 1 a 5 tareas.
- Las pruebas de integración en CI usan una base en contenedor dentro del runner, no la de la nube.
- La base de la nube empieza vacía (no hay semilla de reportes).
- Se trabaja en una rama `aws-provisioning` con PR a `main`, como Geoespacial y Usuarios (nada se despliega hasta el merge).

## 10. Métricas de éxito

- **MS-001**: Desde un commit a `main` hasta la nueva versión respondiendo por el gateway pasan menos de 20 minutos sin acción manual.
- **MS-002**: Por la URL del gateway: se crea un reporte con coordenada (con `idCoordenada` de Geoespacial) y otro con imagen (con `urlImagen` de Multimedia que abre la imagen); se listan, consultan, cambian de estado, rechazan y borran con los mismos códigos que en local.
- **MS-003**: Tras crear un reporte, el evento aparece en el bus (comprobable con la métrica de eventos publicados del bus o un destino de prueba) con el contenido acordado.
- **MS-004**: Un segundo aprovisionamiento sin cambios reporta 0 recursos modificados.
- **MS-005**: Ningún secreto aparece en el repositorio, la imagen publicada ni los logs de los flujos.
- **MS-006**: Tras desaprovisionar Reportes, las rutas de Geoespacial, Usuarios y Multimedia siguen respondiendo.

## 11. Clarificaciones

### Sesión 2026-10-11
- P: ¿Qué bus de EventBridge? → R: `arn:aws:events:us-east-2:348364742283:event-bus/urban-alert-bus-eventos` (creado a mano; RF-012).
- P: ¿Formato del evento? → R: el propuesto: `source = urban-alert.reportes`, `detail-type = ReporteCreado`, `detail` = `ReporteCreadoEvent` actual en JSON con enums como códigos (RF-012, HU-4.4). Auditoria se adaptará en su repo.
- P: ¿Autorización de las escrituras? → R: **todas las rutas públicas**, como en local (RF-021, HU-6.1); el frontend crea y gestiona reportes directamente.
- P: ¿Lectura de la base por Auditoria? → R: **fuera de alcance**; solo las tareas de Reportes llegan a la base (HU-3.3).

---

### Checklist de calidad (autorrevisión antes de entregar)

- [x] Sin detalles de implementación — AWS, GitHub Actions, Terraform, ECS Express, PostgreSQL y EventBridge son restricciones fijadas por el usuario y la plataforma, no decisiones de diseño
- [x] Cada requisito es testeable y no ambiguo
- [x] Criterios de aceptación con resultado observable
- [x] Casos borde identificados
- [x] Alcance acotado (fuera de alcance explícito)
- [x] Como máximo 3 `[NEEDS CLARIFICATION]` pendientes (0 tras la sesión del 2026-10-11)
- [x] Coherente con `.sdd/constitution.md` 1.0.0
