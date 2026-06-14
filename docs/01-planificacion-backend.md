# CampusConnect 360 — Planificación del Backend

Documento de trabajo. Define la arquitectura del backend antes de escribir código: microservicios, kernel compartido, Clean Architecture, CQRS, comunicación síncrona (Ocelot + HTTP) y asíncrona (MassTransit/RabbitMQ).

## 1. Decisiones tomadas

| Decisión | Elección | Motivo |
|---|---|---|
| Topología de datos | Base de datos por microservicio | Aislamiento real; cumple el requisito de separación; la consistencia entre servicios se logra por eventos |
| API Gateway | Ocelot | Gateway .NET, configuración por JSON, validación JWT en el borde |
| Autenticación | Servicio Identity dedicado | Emisor central de JWT; el resto valida el token |
| Mensajería | RabbitMQ + MassTransit | Requisito obligatorio; MassTransit cubre pub/sub, point-to-point, retry, DLQ, outbox/inbox |
| Arquitectura interna | Clean Architecture (API / Application / Domain / Infrastructure) en todos los servicios | Requisito del grupo; reglas de capas no negociables |
| CQRS | Lógico con MediatR, una sola base por servicio | Separación command/query en Application, sin read-store físico aparte. No hay sincronización entre dos bases |
| Reutilización | Kernel compartido por project reference | Centraliza comportamiento transversal, no lógica de dominio |

Aclaración sobre CQRS: la separación es a nivel de aplicación (handlers de Command vs Query). No existe una segunda base de lectura por servicio, por lo que no hay problema de sincronización dentro del servicio. La "vista analítica" exigida por la consigna se resuelve a nivel de sistema con el servicio de Analítica, que mantiene un modelo de lectura propio alimentado por eventos.

## 2. Vista general del ecosistema

```
                         Frontends React
        (Portal Académico, Portal Financiero, Portal Docente, Dashboard)
                                  |
                                  | HTTP (JWT)
                                  v
                        +--------------------+
                        |  API Gateway       |  Ocelot
                        |  (valida JWT)      |
                        +--------------------+
            ____________________|____________________________
           |        |          |          |          |       |
           v        v          v          v          v       v
       Identity  Académico   Pagos    Asistencia  Notif.  Analítica
        (Auth)                        /Bienestar
          |        |          |          |          |       |
        idn_db   acad_db    pay_db     att_db     ntf_db  anl_db
           |        |          |          |          |       |
           +--------+----------+----------+----------+-------+
                                  |
                                  v
                        +--------------------+
                        |     RabbitMQ       |  (MassTransit)
                        |  exchanges/queues  |
                        +--------------------+
```

Dos planos de comunicación:

- Plano síncrono (HTTP/REST): frontends hacia el Gateway, y llamadas puntuales servicio a servicio cuando se necesita respuesta inmediata (con Polly: timeout, retry, circuit breaker).
- Plano asíncrono (eventos): publicación de eventos de negocio en RabbitMQ vía MassTransit; varios servicios reaccionan sin acoplarse.

## 3. Microservicios

Seis servicios de negocio/soporte más el Gateway. Cada uno con responsabilidad única, base propia y las cuatro capas de Clean Architecture.

| Servicio | Responsabilidad | Base | Publica | Consume |
|---|---|---|---|---|
| Identity | Usuarios, roles (Secretaría, Finanzas, Docente, Dirección), emisión y refresco de JWT | identity_db | — | — |
| Académico | Estudiantes, matrículas, estado académico y financiero del estudiante | academic_db | StudentEnrolled, StudentStatusUpdated | PaymentConfirmed |
| Pagos | Obligaciones de pago, deuda simulada, confirmación de pagos | payments_db | PaymentConfirmed | StudentEnrolled (réplica mínima de estudiantes) |
| Asistencia/Bienestar | Registro de asistencia, ausencias e incidentes | attendance_db | AttendanceRecorded, IncidentReported | StudentEnrolled (réplica mínima de estudiantes) |
| Notificaciones | Genera notificaciones simuladas ante eventos de negocio | notifications_db | NotificationSent, NotificationFailed | StudentEnrolled, PaymentConfirmed, IncidentReported, AttendanceRecorded |
| Analítica | Modelo de lectura consolidado para el dashboard directivo | analytics_db | — | Todos los eventos |
| API Gateway (Ocelot) | Entrada centralizada, ruteo, validación JWT en el borde | — | — | — |

Regla de oro: ningún servicio accede a la base de otro. Si Pagos o Asistencia necesitan datos de estudiantes para listarlos, mantienen una réplica mínima local (id, nombre, grado, estado) alimentada por el evento StudentEnrolled. Esto evita acoplamiento por base y permite listar sin llamadas síncronas.

### 3.1 Capas por servicio (Clean Architecture)

Dirección de dependencias, no negociable:

```
Domain          -> (sin dependencias)
Application     -> Domain
Infrastructure  -> Application + Domain
API             -> Application (+ Infrastructure solo para composición/DI)
```

Contenido de cada capa:

- Domain: entidades y agregados, value objects, eventos de dominio internos, excepciones de dominio, extensiones sobre IQueryable. Sin referencias a DTOs ni a tipos de infraestructura.
- Application: casos de uso como Command o Query (MediatR), handlers, validadores FluentValidation, DTOs, interfaces (repositorios, servicios), pipeline behaviors. Consume los contratos de eventos del kernel.
- Infrastructure: DbContext de EF Core, implementación de repositorios, consumidores MassTransit, configuración de outbox/inbox, clientes HTTP hacia otros servicios, health checks.
- API: controllers o minimal APIs, configuración de JWT, Swagger/OpenAPI, resolución de identidad desde los claims, traducción de request a Command/Query. Sin lógica de negocio.

Violaciones a vigilar: tipos de infraestructura (HttpContext, tipos de EF, IFormFile) nunca aparecen en Application o Domain; los handlers no inyectan IHttpContextAccessor; la identidad (userId, schoolId) se resuelve en la capa API desde el JWT y se pasa explícita al Command.

## 4. Kernel compartido (BuildingBlocks)

Proyectos consumidos por project reference. Centraliza comportamiento transversal, no reglas de negocio. Un kernel que contenga dominio acoplaría los servicios y es atacable en la defensa; por eso se limita a contratos e infraestructura técnica.

Se divide en cuatro proyectos para respetar las capas:

- BuildingBlocks.Contracts: contratos de eventos de integración (records inmutables). Lo referencian publicadores y consumidores. Es el único contrato compartido entre servicios y debe mantenerse estable y versionado.
- BuildingBlocks.Domain: clases base Entity, AggregateRoot, ValueObject, interfaz IDomainEvent, DomainException base.
- BuildingBlocks.Application: tipo Result<T> y Result, marcadores ICommand/IQuery, pipeline behaviors de MediatR (Validation, Logging, UnitOfWork), contexto de correlación (CorrelationContext).
- BuildingBlocks.Infrastructure: método de extensión para configurar MassTransit de forma uniforme (transporte, retry, redelivery, DLQ, outbox/inbox), configuración base de DbContext con outbox, Serilog y health checks comunes.

Cada microservicio referencia los proyectos del kernel que le corresponden según su capa. Por ejemplo, Application del servicio referencia BuildingBlocks.Application y BuildingBlocks.Contracts; Infrastructure referencia BuildingBlocks.Infrastructure.

### 4.1 Contrato de evento (base común)

Todos los eventos heredan estos campos, exigidos por la consigna:

```csharp
public abstract record IntegrationEvent
{
    public Guid EventId { get; init; } = Guid.NewGuid();
    public string EventType => GetType().Name;
    public DateTime OccurredAt { get; init; } = DateTime.UtcNow;
    public string CorrelationId { get; init; } = default!;
}
```

Ejemplo concreto:

```csharp
public record StudentEnrolled : IntegrationEvent
{
    public string StudentId { get; init; } = default!;
    public string SchoolId { get; init; } = default!;
    public string Grade { get; init; } = default!;
    public string FullName { get; init; } = default!;
}
```

## 5. Comunicación síncrona (Ocelot + HTTP)

### 5.1 API Gateway con Ocelot

Punto de entrada único para los frontends. Responsabilidades: ruteo a los servicios internos, validación del JWT en el borde, y opcionalmente rate limiting y agregación.

Esquema de ruteo (ocelot.json, resumido):

```
/api/identity/*    -> identity-service:8080
/api/academic/*    -> academic-service:8080
/api/payments/*    -> payments-service:8080
/api/attendance/*  -> attendance-service:8080
/api/notifications/* -> notifications-service:8080
/api/analytics/*   -> analytics-service:8080
```

El Gateway valida el JWT (firma, expiración, emisor) antes de rutear. Los servicios vuelven a validar el token y autorizan por rol, de modo que la seguridad no depende solo del borde.

### 5.2 Llamadas síncronas servicio a servicio

La mayoría de la coordinación es asíncrona por eventos. Se reserva HTTP síncrono para los casos donde se necesita respuesta inmediata y consistencia fuerte. Caso principal:

- Confirmación de pago: antes de registrar el pago, el servicio de Pagos llama por HTTP a Académico (GET /api/academic/students/{id}/status) para verificar que el estudiante existe y está activo. Es una validación que requiere respuesta inmediata.

Estas llamadas usan IHttpClientFactory con Polly: timeout, retry con backoff y circuit breaker. Si Académico no responde, el circuit breaker abre y Pagos degrada de forma controlada (sirve también como escenario de falla síncrona).

## 6. Comunicación asíncrona (MassTransit / RabbitMQ)

### 6.1 Configuración interna uniforme

Definida una sola vez en BuildingBlocks.Infrastructure y aplicada por cada servicio:

- Transporte RabbitMQ con `cfg.ConfigureEndpoints(context)` para mapear consumidores a colas automáticamente.
- Reintentos: `UseMessageRetry` con backoff incremental o exponencial (por ejemplo 3 intentos).
- Redelivery diferido: `UseDelayedRedelivery` para fallos transitorios prolongados.
- Outbox/Inbox transaccional de EF Core para publicación confiable e idempotencia.
- Nombres de colas y exchanges con convención por servicio (kebab-case).

### 6.2 Pub/Sub vs Point-to-Point

Ambos patrones se evidencian de forma deliberada:

- Publish/Subscribe (`Publish`): los eventos de negocio se publican a un exchange por tipo de mensaje; MassTransit los entrega a la cola de cada consumidor suscrito. Un mismo evento (por ejemplo PaymentConfirmed) lo reciben en paralelo Académico, Notificaciones y Analítica. Cada uno tiene su propia cola.
- Point-to-Point (`Send`): un comando se envía a la cola de un único consumidor. Ejemplo: el despacho concreto de una notificación se modela como comando `SendNotificationCommand` enviado a la cola de Notificaciones; lo procesa un solo consumidor. El evento posterior NotificationSent sí es pub/sub.

Distinción clave a defender: un evento (hecho ocurrido, varios interesados) se publica; un comando (instrucción a un destinatario) se envía. Esto separa Event Message de Command Message y demuestra los dos estilos de canal.

### 6.3 Resiliencia y DLQ

- MassTransit crea automáticamente una cola `<consumer>_error` cuando un mensaje agota los reintentos: ese es el Dead Letter Channel. Los mensajes no consumidos van a `_skipped`.
- Idempotencia (Idempotent Receiver): el Inbox transaccional deduplica por MessageId, evitando reprocesar un evento duplicado. Se aplica al menos al flujo crítico de pagos.
- Outbox: garantiza que el evento se publica solo si la transacción de negocio se confirmó, evitando inconsistencias entre la base y el bus.
- Mensaje con formato inválido: cae en `_error` tras los reintentos; se puede reprocesar moviéndolo de vuelta a la cola original (Shovel de RabbitMQ o reenvío manual).

## 7. Mapa de eventos

| Evento | Publicador | Consumidores | Patrón |
|---|---|---|---|
| StudentEnrolled | Académico | Notificaciones, Analítica, Pagos, Asistencia | Pub/Sub |
| PaymentConfirmed | Pagos | Académico, Notificaciones, Analítica | Pub/Sub |
| AttendanceRecorded | Asistencia | Analítica, (Notificaciones cuando aplique) | Pub/Sub |
| IncidentReported | Asistencia | Notificaciones, Analítica | Pub/Sub |
| StudentStatusUpdated | Académico | Analítica | Pub/Sub |
| NotificationSent | Notificaciones | Analítica | Pub/Sub |
| NotificationFailed | Notificaciones | Analítica | Pub/Sub |
| SendNotificationCommand | Servicios de negocio | Notificaciones | Point-to-Point |

Son siete eventos de negocio más un comando, por encima del mínimo de cuatro. El CorrelationId se propaga en toda la cadena para trazar un flujo completo (por ejemplo: StudentEnrolled -> SendNotificationCommand -> NotificationSent).

## 8. Mapeo a los patrones exigidos (consigna sección 10)

| Patrón | Dónde se evidencia |
|---|---|
| API Gateway | Ocelot como entrada centralizada |
| Publish/Subscribe | Eventos de negocio con múltiples consumidores |
| Point-to-Point | SendNotificationCommand a un único consumidor |
| Message Channel | Colas y exchanges nombrados por convención |
| Event Message | Records que heredan de IntegrationEvent |
| Message Translator | Mapeo evento -> modelo local (réplica de estudiantes en Pagos/Asistencia; proyección en Analítica) |
| Idempotent Receiver | Inbox transaccional de MassTransit en el flujo de pagos |
| Dead Letter Channel | Colas _error de MassTransit |
| CQRS / vista analítica | MediatR por servicio; servicio de Analítica como modelo de lectura |
| Health Check API | Endpoint /health por servicio, agregado |
| Logs / trazabilidad | Serilog estructurado con CorrelationId |

## 9. Estructura de la solución .NET

```
CampusConnect360.sln
│
├── src/
│   ├── BuildingBlocks/
│   │   ├── BuildingBlocks.Contracts/
│   │   ├── BuildingBlocks.Domain/
│   │   ├── BuildingBlocks.Application/
│   │   └── BuildingBlocks.Infrastructure/
│   │
│   ├── Gateway/
│   │   └── CampusConnect.Gateway/            (Ocelot)
│   │
│   └── Services/
│       ├── Identity/
│       │   ├── Identity.API/
│       │   ├── Identity.Application/
│       │   ├── Identity.Domain/
│       │   └── Identity.Infrastructure/
│       ├── Academic/        (misma estructura de 4 capas)
│       ├── Payments/
│       ├── Attendance/
│       ├── Notifications/
│       └── Analytics/
│
├── docker-compose.yml
└── docs/
```

Cada servicio es una mini-solución de cuatro proyectos. El Gateway y el kernel viven fuera de Services. La regla de project reference: API -> Application -> Domain, Infrastructure -> Application/Domain, y referencias al kernel según la capa correspondiente.

## 10. Escenario de falla obligatorio

Plan para el requisito de resiliencia (consigna sección 11). Se demostrará el servicio de Notificaciones caído:

1. Se dispara un evento de negocio (por ejemplo PaymentConfirmed) con Notificaciones apagado.
2. El evento queda durable en la cola de Notificaciones en RabbitMQ; el resto del sistema (Académico, Analítica) sigue operando. Store-and-forward.
3. Al levantar Notificaciones, consume los eventos pendientes y genera las notificaciones atrasadas.
4. Variante con mensaje inválido o duplicado: el duplicado es descartado por el Inbox (idempotencia); el inválido agota reintentos y cae en la cola _error (DLQ), visible en el panel de RabbitMQ.

Durante la defensa se explica qué ocurre con el mensaje en cada caso: persistido en cola, deduplicado por Inbox, o movido a DLQ para reprocesamiento.

## 11. Próximos pasos

1. Validar este plan y ajustar nombres/alcance de servicios.
2. Detallar contratos de API por servicio (endpoints, request/response) y contratos de eventos completos.
3. Definir el modelo de dominio de cada servicio (agregados, invariantes).
4. Diseñar el docker-compose (servicios, RabbitMQ, bases por servicio, healthchecks).
5. Esqueleto de solución .NET y kernel compartido.
