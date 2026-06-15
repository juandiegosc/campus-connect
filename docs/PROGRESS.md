# CampusConnect 360 — Progreso SDD

## Fase completada

**Cambio**: `backend-bootstrap` (Phase 1 del backend)
**Estado**: COMPLETO — Fases 1–10 de 10 completadas.
**Fecha**: 2026-06-14
**SDK activo**: .NET 10.0.101

---

## Archivos clave

### Fase 1 — Tooling base del repo

| Archivo | Descripción |
|---|---|
| `global.json` | SDK 10.0.101 con rollForward: latestFeature |
| `Directory.Packages.props` | Central Package Management (ManagePackageVersionsCentrally=true) |
| `Directory.Build.props` (raíz) | LangVersion=latest, Nullable=enable, ImplicitUsings=enable, net10.0 |
| `src/BuildingBlocks/Directory.Build.props` | TreatWarningsAsErrors=true, EnforceCodeStyleInBuild=true, GenerateDocumentationFile=true |
| `.editorconfig` | file-scoped namespaces, 4-space indent, IDE0005 como warning |
| `.env.example` | 7 variables de entorno (DB_USER, DB_PASSWORD, RABBITMQ_USER, RABBITMQ_PASS, JWT_SIGNING_KEY, JWT_ISSUER, JWT_AUDIENCE) |
| `build/Dockerfile.template` | Actualizado sdk:9.0 → sdk:10.0, aspnet:9.0 → aspnet:10.0 |

### Fase 2 — Solución y proyectos del kernel

| Archivo | Descripción |
|---|---|
| `CampusConnect360.sln` | Solución con 11 proyectos (4 kernel + Gateway + 6 stubs) |
| `src/BuildingBlocks/BuildingBlocks.Contracts/BuildingBlocks.Contracts.csproj` | classlib net10.0, sin PackageReferences |
| `src/BuildingBlocks/BuildingBlocks.Domain/BuildingBlocks.Domain.csproj` | classlib net10.0, sin PackageReferences |
| `src/BuildingBlocks/BuildingBlocks.Application/BuildingBlocks.Application.csproj` | refs Domain + Contracts; MediatR, FluentValidation, Serilog |
| `src/BuildingBlocks/BuildingBlocks.Infrastructure/BuildingBlocks.Infrastructure.csproj` | refs Application + Contracts; FrameworkReference AspNetCore.App; MassTransit, EF Core, Serilog |

### Fase 3 — BuildingBlocks.Contracts

| Archivo | Descripción |
|---|---|
| `Events/IntegrationEvent.cs` | abstract record base (EventId, EventType, OccurredAt, CorrelationId) |
| `Events/StudentEnrolled.cs` | record : IntegrationEvent |
| `Events/PaymentConfirmed.cs` | record : IntegrationEvent |
| `Events/AttendanceRecorded.cs` | record : IntegrationEvent |
| `Events/IncidentReported.cs` | record : IntegrationEvent |
| `Events/StudentStatusUpdated.cs` | record : IntegrationEvent |
| `Events/NotificationSent.cs` | record : IntegrationEvent |
| `Events/NotificationFailed.cs` | record : IntegrationEvent |
| `Commands/SendNotificationCommand.cs` | record : IntegrationEvent |
| `Serialization/IntegrationEventJsonContext.cs` | JsonSerializerContext con 8 [JsonSerializable] |
| `Abstractions/IIntegrationEventFactory.cs` | interface con Create<T>(string correlationId) |

### Fase 4 — BuildingBlocks.Domain

| Archivo | Descripción |
|---|---|
| `Primitives/Entity.cs` | abstract class Entity<TId> con Equals/GetHashCode por Id |
| `Primitives/AggregateRoot.cs` | hereda Entity<TId>; expone Raise(IDomainEvent) y ClearDomainEvents() |
| `Primitives/ValueObject.cs` | abstract con GetEqualityComponents() |
| `Events/IDomainEvent.cs` | marker interface |
| `Exceptions/DomainException.cs` | hereda Exception |

### Fase 5 — BuildingBlocks.Application

| Archivo | Descripción |
|---|---|
| `Common/ErrorType.cs` | enum con 6 valores |
| `Common/Error.cs` | readonly record struct con 6 factory methods + None |
| `Common/Result.cs` | Result + Result<T> con Match<TOut> |
| `Messaging/ICommand.cs` | IBaseCommand, ICommand, ICommand<TResponse> |
| `Messaging/IQuery.cs` | IBaseQuery, IQuery<TResponse> |
| `Abstractions/IUnitOfWork.cs` | SaveChangesAsync(CancellationToken) |
| `Correlation/ICorrelationContext.cs` | string CorrelationId { get; } |
| `Behaviors/ValidationBehavior.cs` | IPipelineBehavior; detiene pipeline en fallo de validación |
| `Behaviors/LoggingBehavior.cs` | IPipelineBehavior; CorrelationId en scope Serilog |
| `Behaviors/UnitOfWorkBehavior.cs` | IPipelineBehavior; constraint IBaseCommand |
| `DependencyInjection.cs` | AddCampusConnectApplication(IServiceCollection, params Assembly[]) |

### Fase 6 — BuildingBlocks.Infrastructure

| Archivo | Descripción |
|---|---|
| `Persistence/BaseDbContext.cs` | abstract DbContext con hook domain events y convención Outbox/Inbox |
| `Correlation/HttpCorrelationContext.cs` | lee IHttpContextAccessor.Items["CorrelationId"] |
| `Correlation/CorrelationIdMiddleware.cs` | lee/genera X-Correlation-Id; escribe en Items y response header |
| `Correlation/CorrelationApplicationBuilderExtensions.cs` | app.UseCampusConnectCorrelation() |
| `Messaging/MassTransitExtensions.cs` | AddCampusConnectMassTransit<TContext>(IBusRegistrationConfigurator, IConfiguration) |
| `Logging/SerilogBootstrap.cs` | AddCampusConnectLogging(WebApplicationBuilder) |
| `HealthChecks/HealthCheckExtensions.cs` | AddCampusConnectHealthChecks(IServiceCollection) → IHealthChecksBuilder |
| `Time/IntegrationEventFactory.cs` | implementa IIntegrationEventFactory con TimeProvider |
| `DependencyInjection.cs` | AddCampusConnectInfrastructure(IServiceCollection, IConfiguration) |

### Fase 7 — Gateway (CampusConnect.Gateway)

| Archivo | Descripción |
|---|---|
| `src/Gateway/CampusConnect.Gateway/CampusConnect.Gateway.csproj` | net10.0 WebApp; refs Ocelot, JwtBearer, Serilog |
| `src/Gateway/CampusConnect.Gateway/Program.cs` | JWT Bearer con placeholder dev key; middleware /health antes de UseOcelot(); Serilog bootstrap |
| `src/Gateway/CampusConnect.Gateway/appsettings.json` | Serilog MinimumLevel: Information |
| `src/Gateway/CampusConnect.Gateway/ocelot.json` | Rutas: /health sin auth + /api/{slug}/health sin auth + /api/{slug}/{everything} con auth (académico y siguientes) |
| `src/Gateway/CampusConnect.Gateway/Dockerfile` | Copia Directory.Packages.props + Directory.Build.props + src/Gateway; multi-stage sdk:10.0/aspnet:10.0 |

### Fase 8 — 6 Service API Stubs

| Servicio | Archivos creados |
|---|---|
| Identity | `src/Services/Identity/Identity.API/{Identity.API.csproj, Program.cs, appsettings.json, Dockerfile}` |
| Academic | `src/Services/Academic/Academic.API/{Academic.API.csproj, Program.cs, appsettings.json, Dockerfile}` |
| Payments | `src/Services/Payments/Payments.API/{Payments.API.csproj, Program.cs, appsettings.json, Dockerfile}` |
| Attendance | `src/Services/Attendance/Attendance.API/{Attendance.API.csproj, Program.cs, appsettings.json, Dockerfile}` |
| Notifications | `src/Services/Notifications/Notifications.API/{Notifications.API.csproj, Program.cs, appsettings.json, Dockerfile}` |
| Analytics | `src/Services/Analytics/Analytics.API/{Analytics.API.csproj, Program.cs, appsettings.json, Dockerfile}` |

Cada stub: `builder.Services.AddOpenApi()` + `app.MapOpenApi()` + `MapGet("/health", ...)` + `MapGet("/api/{slug}/health", ...)` (dual-health per ADR-013).

### Archivos adicionales

| Archivo | Descripción |
|---|---|
| `docker-compose.local.yml` | Override local: elimina host port bindings conflictivos de rabbitmq (5672/15672) e identity-db (5433) para entornos con otros proyectos activos |

---

## Decisiones tomadas (resumen de ADRs)

| ADR | Decisión |
|---|---|
| ADR-001 | Target net10.0 en nuestros proyectos; roll-forward binario para MassTransit/Ocelot vía AssetTargetFallback |
| ADR-002 | Central Package Management; versiones inciertas resueltas con dotnet add package |
| ADR-003 | Result<T> + readonly record struct Error con 6 factory methods |
| ADR-004 | abstract record IntegrationEvent + JsonSerializerContext source-gen |
| ADR-005 | TimeProvider.System (Singleton) + IIntegrationEventFactory para estampar OccurredAt testeable |
| ADR-006 | Pipeline: LoggingBehavior → ValidationBehavior → UnitOfWorkBehavior; IBaseCommand como constraint |
| ADR-007 | CorrelationIdMiddleware en Infrastructure; ICorrelationContext como Scoped |
| ADR-008 | BaseDbContext abstracto con hook para domain events (Phase 2 wiring) |
| ADR-009 | Health checks: solo liveness en Phase 1 |
| ADR-010 | Serilog 4.x con RenderedCompactJsonFormatter en Console |
| ADR-011 | OpenAPI nativo (Microsoft.AspNetCore.OpenApi), sin Swashbuckle |
| ADR-012 | Gateway JWT + ocelot.json; health via middleware antes de UseOcelot() |
| ADR-013 | Stubs con dual health /health + /api/{slug}/health |
| ADR-014 | Dockerfile.template actualizado a sdk:10.0 / aspnet:10.0 |
| ADR-015 | TreatWarningsAsErrors=true solo en src/BuildingBlocks/; stubs con false |

---

## Versiones reales de paquetes resueltas

| Paquete | Versión | Notas |
|---|---|---|
| MediatR | 12.4.1 | Estable en 12.x |
| FluentValidation | 11.11.0 | |
| FluentValidation.DependencyInjectionExtensions | 11.11.0 | |
| MassTransit | 8.3.6 | net10.0 via roll-forward desde net9.0; NU1701 NO emitido (compatible) |
| MassTransit.RabbitMQ | 8.3.6 | |
| MassTransit.EntityFrameworkCore | 8.3.6 | |
| Microsoft.EntityFrameworkCore | 10.0.0 | |
| Microsoft.AspNetCore.OpenApi | 10.0.0 | OpenAPI nativo, sin Swashbuckle |
| Microsoft.AspNetCore.Authentication.JwtBearer | 10.0.0 | |
| Serilog | 4.2.0 | (transitive via Serilog.AspNetCore) |
| Serilog.AspNetCore | 9.0.0 | |
| Serilog.Formatting.Compact | 3.0.0 | |
| Ocelot | 23.4.2 | net10.0 via roll-forward desde net9.0; OK en runtime |
| Npgsql.EntityFrameworkCore.PostgreSQL | 10.0.0 | |
| .NET SDK activo | 10.0.101 | rollForward: latestFeature |

---

## Gotchas y decisiones técnicas

### Gotcha 1 — IDE0005 + EnforceCodeStyleInBuild
Para que `IDE0005` (unused usings) funcione como error en build, se requiere `GenerateDocumentationFile=true` en el `Directory.Build.props` de BuildingBlocks. Sin ello, MSBuild lanza un error `EnableGenerateDocumentationFile`. Añadido + `<NoWarn>CS1591</NoWarn>` para suprimir advertencias de XML doc faltante en miembros públicos.

### Gotcha 2 — MediatR 12.x RequestHandlerDelegate
En MediatR 12.x el delegado `RequestHandlerDelegate<TResponse>` es `Func<Task<TResponse>>` (sin `CancellationToken`). El código incorrecto `await next(cancellationToken)` falla con CS1593. La firma correcta es `await next()`.

### Gotcha 3 — Microsoft.Extensions.Diagnostics.HealthChecks redundante
Al usar `<FrameworkReference Include="Microsoft.AspNetCore.App" />`, el paquete `Microsoft.Extensions.Diagnostics.HealthChecks` ya está incluido en el framework. Añadirlo explícitamente genera `NU1510` (warning as error). Se eliminó del `<ItemGroup>` de PackageReferences.

### Gotcha 4 — NETSDK1022: Content items duplicados en Gateway
`<Content Include="ocelot.json">` falla con NETSDK1022 porque el SDK de .NET ya incluye todos los archivos de Content por defecto. La solución es usar `<Content Update="ocelot.json">` para sobreescribir solo la propiedad `CopyToOutputDirectory`.

### Gotcha 5 — Ocelot swallows all routes: UseOcelot() termina el pipeline
`await app.UseOcelot()` llama `app.Run()` internamente. Cualquier `MapGet()` registrado ANTES queda inaccesible porque el routing de minimal API no se ejecuta. La solución es registrar el endpoint `/health` como middleware (`app.Use(async (ctx, next) => { ... })`) ANTES de `UseOcelot()`.

### Gotcha 6 — SymmetricSecurityKey con clave vacía en dev
`SymmetricSecurityKey` lanza `IDX10703` si la clave tiene 0 bytes. Cuando `JWT_SIGNING_KEY` no está configurado (dev bootstrap sin `.env`), se usa un placeholder de 32 bytes para que el Gateway arranque. En producción, `JWT_SIGNING_KEY` DEBE configurarse via env var o secrets manager.

### Gotcha 7 — Directory.Packages.props ausente en Docker build
El Dockerfile solo copiaba `src/Services/<Servicio>/` pero `Directory.Packages.props` vive en la raíz del repo. Sin ese archivo, `dotnet restore` falla con `NU1015: PackageReference items do not have a version specified`. Solución: añadir `COPY Directory.Packages.props .` y `COPY Directory.Build.props .` en todos los Dockerfiles antes del restore.

### Gotcha 8 — Rutas /api/{slug}/health bloqueadas por AuthenticationOptions en ocelot.json
Las rutas `/api/academic/{everything}`, `/api/payments/{everything}`, etc. tienen `AuthenticationOptions` que requieren un Bearer token. Esto bloqueaba los health probes sin token (401). Solución: añadir rutas específicas `/api/{slug}/health` SIN `AuthenticationOptions` ANTES de las rutas genéricas `/{everything}` en `ocelot.json`. Ocelot evalúa rutas en orden de aparición.

### Gotcha 9 — Conflictos de puertos en entorno multi-proyecto
El entorno del desarrollador tenía `wax-rabbitmq` ocupando 5672/15672 y `wax-postgres` ocupando 5433. Solución: `docker-compose.local.yml` que elimina los host port bindings conflictivos. Los contenedores siguen comunicándose correctamente via la red Docker interna `campusnet`.

---

## Verificación e2e completada: 2026-06-14

### Resultado de curl probes

```
GET http://localhost:8080/health
→ 200 {"status":"ok","service":"gateway"}

GET http://localhost:8080/api/identity/health
→ 200 {"status":"ok","service":"identity"}

GET http://localhost:8080/api/academic/health
→ 200 {"status":"ok","service":"academic"}

GET http://localhost:8080/api/payments/health
→ 200 {"status":"ok","service":"payments"}

GET http://localhost:8080/api/attendance/health
→ 200 {"status":"ok","service":"attendance"}

GET http://localhost:8080/api/notifications/health
→ 200 {"status":"ok","service":"notifications"}

GET http://localhost:8080/api/analytics/health
→ 200 {"status":"ok","service":"analytics"}
```

### Build verification final
- `dotnet restore CampusConnect360.sln`: 0 errores
- `dotnet build CampusConnect360.sln --no-incremental`: 0 errores, 0 warnings (11 proyectos: 4 kernel + Gateway + 6 stubs)
- Todos los TreatWarningsAsErrors=true en src/BuildingBlocks/ pasaron sin warnings
- 7 imágenes Docker construidas y corriendo exitosamente

---

## Próximo cambio SDD

**Cambio**: `identity-service` (Phase 2 del backend)

Implementar el servicio de Identidad completo:
- Domain: entidades User, Role, Permission
- Application: comandos RegisterUser, LoginUser, etc.
- Infrastructure: IdentityDbContext (Npgsql), repositorios
- API: endpoints de autenticación con JWT
- Tests de integración con Testcontainers.PostgreSql
