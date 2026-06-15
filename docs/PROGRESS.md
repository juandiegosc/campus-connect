# CampusConnect 360 — Progreso SDD

## Fase completada

**Cambio**: `backend-bootstrap` (Phase 1 del backend)
**Estado**: PARCIAL — Fases 1–6 de 10 completadas en esta sesión.
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
| `CampusConnect360.sln` | Solución con 4 proyectos kernel |
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
| ADR-012 | Gateway JWT + ocelot.json (Phase 7 — pendiente) |
| ADR-013 | Stubs con dual health /health + /api/{slug}/health (Phase 8 — pendiente) |
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
| Serilog | 4.2.0 | (transitive via Serilog.AspNetCore) |
| Serilog.AspNetCore | 9.0.0 | |
| Serilog.Formatting.Compact | 3.0.0 | |
| Ocelot | 23.4.2 | PENDIENTE — no instalado aún (Phase 7) |
| Npgsql.EntityFrameworkCore.PostgreSQL | 10.0.0 | PENDIENTE — no instalado aún (Phase 7/8) |
| .NET SDK activo | 10.0.101 | rollForward: latestFeature |

### Gotcha detectado — IDE0005 con EnforceCodeStyleInBuild
Para que `IDE0005` (unused usings) funcione como error en build, se requiere `GenerateDocumentationFile=true` en el `Directory.Build.props` de BuildingBlocks. Sin ello, MSBuild lanza un error `EnableGenerateDocumentationFile`. Añadido + `<NoWarn>CS1591</NoWarn>` para suprimir advertencias de XML doc faltante en miembros públicos.

### Gotcha detectado — MediatR 12.x RequestHandlerDelegate
En MediatR 12.x el delegado `RequestHandlerDelegate<TResponse>` es `Func<Task<TResponse>>` (sin `CancellationToken`). El código incorrecto `await next(cancellationToken)` falla con CS1593. La firma correcta es `await next()`.

### Gotcha detectado — Microsoft.Extensions.Diagnostics.HealthChecks redundante
Al usar `<FrameworkReference Include="Microsoft.AspNetCore.App" />`, el paquete `Microsoft.Extensions.Diagnostics.HealthChecks` ya está incluido en el framework. Añadirlo explícitamente genera `NU1510` (warning as error). Se eliminó del `<ItemGroup>` de PackageReferences.

---

## Próxima fase

**Comando para retomar**:
```
/sdd-apply backend-bootstrap
```
Lee `sdd/backend-bootstrap/apply-progress` en engram para continuar exactamente donde quedó.

**Fases pendientes**:
- Fase 7: Gateway project (`CampusConnect.Gateway.csproj`, `Program.cs` con Ocelot + JWT, `appsettings.json`, `Dockerfile`)
- Fase 8: 6 service API stubs con dual-health (`/health` + `/api/{slug}/health`), OpenAPI nativo, Dockerfiles
- Fase 9: Verificación end-to-end via `docker compose --profile services up -d --build` + curl probes
- Fase 10: Actualizar este `PROGRESS.md` + `CLAUDE.md` a estado FINAL, con versiones de Ocelot y Npgsql resueltas

**Próximo cambio SDD** (después de backend-bootstrap): `identity-service` (Phase 2)
