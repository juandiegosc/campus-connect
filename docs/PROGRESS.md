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

---

## Fase 2 — identity-service (Domain + Infra mínima)

**Cambio**: `identity-service` (Phase 2 del backend)
**Estado**: COMPLETO — 37/37 tasks completadas
**Fecha**: 2026-06-15
**Build**: 0 errores, 4 advertencias NU1903 (transitivas de BCrypt en Identity.Infrastructure — no en BuildingBlocks)
**Tests**: 12/12 dotnet test pasaron (PasswordHashTests + UserTests)
**Smoke e2e**: 4/4 verdes (POST 201, POST duplicado 409, POST inválido 400, GET /health 200)

### Alcance entregado
- Kernel patch: `IAggregateRoot` marker + dispatch real de domain events en `BaseDbContext` (save → dispatch → clear)
- ADR-023: `BaseDbContext` implementa `IUnitOfWork`
- ADR-024: BCrypt cost factor 12
- `Identity.Domain`: User aggregate + UserId + PasswordHash VOs + UserRole enum + UserCreatedDomainEvent
- `Identity.Application`: IUserRepository, IPasswordHasher, RegisterUserCommand + handler + validator + DI
- `Identity.Infrastructure`: IdentityDbContext + UserConfiguration (snake_case + unique idx username) + BcryptPasswordHasher + UserRepository + IDesignTimeDbContextFactory + DI
- EF Migration `InitialCreate` → tabla `users` aplicada
- `Identity.API`: endpoint público `POST /api/identity/users` (proyecto local-only) + Npgsql health check + DI completa
- `Identity.Tests` (xunit.v3 + FluentAssertions): 4 tests PasswordHash + 8 tests User

### Decisiones registradas
- Identity NO se integra con MassTransit (cero eventos publicados/consumidos en Phase 2 — ADR-019)
- `POST /api/identity/users` es PÚBLICO (sin auth) — proyecto local-only por decisión explícita en CLAUDE.md
- Tests sólo de Domain (xunit.v3 + FluentAssertions). Testcontainers se pospone a Phase 3 cuando aparezca login.

### Gotchas adicionales descubiertos durante apply

**Gotcha 10 — MediatR IPublisher requiere dispatcher con DLLs sincronizadas**: Si `BuildingBlocks.Domain.dll` deployado en el contenedor es de un build anterior a que `IDomainEvent` extendiera `MediatR.INotification`, el `Publish(object)` falla con `ArgumentException: notification does not implement $INotification`. Solución: `docker compose build` SIN cache problem + `docker rm -f` del contenedor antes de `up`.

**Gotcha 11 — `USERNAME` es env var reservada en macOS**: Al hacer smoke tests con `USERNAME="x"` en shell el valor queda como el del usuario del sistema (`jotade`). Usar otro nombre de variable (`U`, `USER_NAME`).

**Gotcha 12 — EF CLI no se conecta a Postgres con SCRAM-SHA-256 desde host**: `dotnet ef database update` falla autenticando contra `cc-identity-db` aún con credenciales correctas. Workaround usado por el sub-agent: aplicar migración manualmente via `docker exec cc-identity-db psql ... -f migration.sql`.

**Gotcha 13 — `xunit.runner.visualstudio` requerido junto a `xunit.v3`**: Sin él, `dotnet test` no descubre los tests por VSTest. Versión usada: 3.1.4.

**Gotcha 14 — `Microsoft.EntityFrameworkCore.Design` requerido también en startup project**: `dotnet ef migrations add` exige el package en `Identity.API.csproj` (startup project), no sólo en `Identity.Infrastructure.csproj`.

**Gotcha 15 — `JsonStringEnumConverter` necesario para deserializar UserRole**: Sin él, payloads JSON con `"role":"Direccion"` fallan parsing. Registrado en `ConfigureHttpJsonOptions` en `Identity.API/Program.cs`.

**Gotcha 16 — `AddIdentityApplication` debe llamar `AddCampusConnectApplication`**: Si Identity.Application sólo registra MediatR sin pipeline behaviors (`UnitOfWorkBehavior` incluido), `SaveChangesAsync` nunca se llama y los usuarios no se persisten. Bug crítico encontrado y corregido pre-merge.

---

## Fase 3 — identity-service (Login + JWT issuing + RefreshToken Rotation + /me + Admin Guard)

**Cambio**: `identity-service-phase3`
**Estado**: COMPLETO — 39/39 tasks completadas
**Fecha**: 2026-06-16
**Build**: `dotnet build CampusConnect360.sln` → 0 errores, 4 advertencias NU1903 (transitivas BCrypt en Identity.Infrastructure — no en BuildingBlocks)
**Tests unitarios**: 37/37 verdes (12 Phase 2 + 11 RefreshToken + 7 LoginCommandHandler + 7 RefreshTokenCommandHandler)
**Tests integración**: 13/13 verdes (Testcontainers.PostgreSql + WebApplicationFactory — full HTTP + real Postgres)
**Total tests**: 50/50 verdes
**Smoke e2e**: 5/5 verdes (login, refresh, /me, POST /users con y sin auth)

### Alcance entregado

**Domain**:
- `RefreshToken` entity (NO aggregate root, no domain events) en `Identity.Domain/RefreshTokens/`
- Factory `Issue(userId, token, expiresAt, nowUtc)` + `Revoke()` + `IsActive(nowUtc)`

**Application**:
- `IUserRepository`: extendido con `FindByUsernameAsync` + `FindByIdAsync`
- `IJwtTokenService` port + `AccessTokenResult` DTO
- `IRefreshTokenRepository` port
- `LoginCommand` (ICommand) + `LoginCommandHandler` + `LoginCommandValidator`
- `RefreshTokenCommand` (ICommand) + `RefreshTokenCommandHandler` + `RefreshTokenCommandValidator`
- `GetCurrentUserQuery` (IQuery) + `GetCurrentUserQueryHandler` (ZERO DB calls — claims only)
- DTOs: `LoginResponse`, `CurrentUserResponse`

**Infrastructure**:
- `JwtTokenService`: HMAC-SHA256, lees `Jwt:*` config, claims: sub/unique_name/name/role/schoolId(SCH-001)/jti/iat
- `RefreshTokenRepository`: sin AsNoTracking (EF tracking requerido para Revoke mutation)
- `UserRepository`: + `FindByUsernameAsync` + `FindByIdAsync`
- `RefreshTokenConfiguration` + migration `AddRefreshTokens` (tabla `refresh_tokens` + FK manual a `users(id)` ON DELETE CASCADE)
- `DependencyInjection.cs`: + `IJwtTokenService`, `IRefreshTokenRepository`, `TimeProvider`

**API**:
- `AuthEndpoints`: POST /api/identity/auth/login (público), POST /api/identity/auth/refresh (público)
- `MeEndpoints`: GET /api/identity/users/me (RequireAuthorization, claims only, no DB)
- `Program.cs`: JWT Bearer + AddAuthorizationBuilder + UseAuthentication/UseAuthorization + public partial class Program {}
- `UserEndpoints`: POST /users ahora requiere `.RequireAuthorization("Direccion")` (**BREAKING CHANGE vs Phase 2**)
- `appsettings.json`: nueva sección `Jwt`

**Tests**:
- `IdentityWebApplicationFactory` (Testcontainers.PostgreSql 4.4.0 + WebApplicationFactory<Program>)
- `AuthFlowIntegrationTests` (13 tests: login/refresh/me/replay/admin-guard/full-flow)

### BREAKING CHANGE: POST /api/identity/users ahora requiere auth

`POST /api/identity/users` ahora requiere `Authorization: Bearer <token>` con `role=Direccion`.
- Sin token → 401
- Token válido pero role ≠ Direccion → 403
- Token Direccion + body válido → 201 (comportamiento anterior)

**Bootstrap SQL** para primer usuario Direccion (si no existe aún):
```sql
-- Generar BCrypt hash primero con: BCrypt.Net.BCrypt.HashPassword("YourPassword", 12)
INSERT INTO users (id, username, full_name, password_hash, role, is_active, created_at)
VALUES (gen_random_uuid(), 'director1', 'Director Principal', '<bcrypt_hash>', 'Direccion', true, NOW())
ON CONFLICT (username) DO NOTHING;
```

### Gotchas adicionales descubiertos durante apply

**Gotcha 17 — EF FK type mismatch (UserId VO vs plain Guid)**:
`HasPrincipalKey(u => u.Id)` falla cuando `User.Id` es `UserId` (VO) y `RefreshToken.UserId` es `Guid`. EF no puede reconciliar tipos CLR distintos. Solución: eliminar `HasOne<User>().WithMany()` de `RefreshTokenConfiguration` y añadir el FK constraint manualmente en el método `Up()` de la migración con `table.ForeignKey(...)`.

**Gotcha 18 — `internal partial class Program {}` causa xUnit1000**:
Si `Program` es `internal`, `WebApplicationFactory<Program>` hereda la accesibilidad, y xUnit requiere que las clases de test sean `public`. Solución: usar `public partial class Program {}` en lugar de `internal`.

**Gotcha 19 — `IQuery<TResponse>` ya wrappea en `Result<TResponse>`**:
`IQuery<TResponse>` se define como `IRequest<Result<TResponse>>`. Usar `IQuery<Result<CurrentUserResponse>>` haría double-wrap → `IRequest<Result<Result<...>>>`. Correcto: `IQuery<CurrentUserResponse>`.

**Gotcha 20 — `RoleClaimType` en `TokenValidationParameters` requerido con claim mapping limpio**:
Al usar `DefaultInboundClaimTypeMap.Clear()` + `MapInboundClaims=false`, el claim `role` no se mapea a `ClaimTypes.Role`. `RequireRole("Direccion")` busca `ClaimTypes.Role` (URI completo) y siempre falla. Solución: `RoleClaimType = "role"` en `TokenValidationParameters`.

**Gotcha 21 — `AddIdentityInfrastructure` captura connection string en tiempo de registro**:
`WebApplicationFactory.ConfigureWebHost` overrides ejecutan DESPUÉS de que `builder.Services.AddIdentityInfrastructure(builder.Configuration)` ya capturó `""` de appsettings.json. El override de `AddInMemoryCollection` no llega a tiempo. Solución en tests: en `ConfigureTestServices`, remover los descriptores de `DbContextOptions<IdentityDbContext>` y `IdentityDbContext`, luego re-registrar con `services.AddDbContext<IdentityDbContext>(opts => opts.UseNpgsql(_container.GetConnectionString()))`.

**Gotcha 22 — `JWT_SIGNING_KEY` env var vacía en docker-compose genera clave vacía**:
`Jwt__SigningKey` recibe `""` desde el env var no configurado. `??` no detecta empty string. `SymmetricSecurityKey(Encoding.UTF8.GetBytes(""))` lanza `IDX10703`. Solución: usar `string.IsNullOrWhiteSpace()` y fallback a la clave dev placeholder.

### Smoke e2e completado: 2026-06-16

```bash
# Login
POST http://localhost:8080/api/identity/auth/login
→ 200 { accessToken, refreshToken, expiresAt, role, fullName }

# Refresh (rotación single-use)
POST http://localhost:8080/api/identity/auth/refresh  
→ 200 { new accessToken, new refreshToken }
# Replay del mismo token:
→ 401

# /me (desde claims, sin DB)
GET http://localhost:8080/api/identity/users/me  [Bearer: <token>]
→ 200 { userId, username, fullName, role }
GET http://localhost:8080/api/identity/users/me  [sin token]
→ 401

# Admin guard POST /users
POST http://localhost:8080/api/identity/users  [sin token]
→ 401
POST http://localhost:8080/api/identity/users  [Bearer: <Direccion token>]
→ 201
```

### Próximo cambio SDD

**Opción A**: `identity-service` Phase 4 — User management (list, deactivate, change password)
**Opción B**: Nuevo bounded context (`academic-service` Phase 1)

---

## Fase 4 — academic-service Phase 1 (Full HTTP Surface + EnrollStudent Outbox Publish)

**Cambio**: `academic-service-phase1`
**Estado**: COMPLETO — 46/46 tasks completadas (Phases 0–13)
**Fecha**: 2026-06-17
**Build**: `dotnet build CampusConnect360.sln` → 0 errores, 4 advertencias NU1903 (transitivas BCrypt en Identity.Infrastructure — no en BuildingBlocks)
**Tests unitarios**: 31/31 verdes (domain VOs, enums, Student aggregate, command handler, query handlers)
**Tests integración**: 19/19 verdes (Testcontainers.PostgreSql + WebApplicationFactory + MassTransit outbox)
**Total tests Academic**: 50/50 verdes
**Smoke e2e**: 4/4 endpoints verdes (POST 201, GET list, GET by id, GET status, GET events)

### Alcance entregado

**BuildingBlocks.Contracts (contratos congelados — one-way door)**:
- `StudentEnrolled`: 5 campos (StudentId, EnrollmentId, SchoolId, Grade, FullName) — ADR-034 delta vs docs/02
- `StudentStatusUpdated`: 3 campos (StudentId, AcademicStatus, FinancialStatus)
- `PaymentConfirmed`: 5 campos (PaymentId, ObligationId, StudentId, Amount, Method)

**Academic.Domain**:
- `StudentId` VO (ULID 26 chars via NUlid 1.7.3)
- `DocumentId` VO (regex ^[A-Za-z0-9]{6,15}$, TryCreate pattern)
- `GuardianContact` VO (Name + Email validation, TryCreate pattern)
- `AcademicStatus` enum (Active, Suspended, Graduated)
- `FinancialStatus` enum (Pending, Paid, Overdue)
- `Enrollment` owned entity
- `Student` aggregate: Create + ConfirmPayment (Pending/Overdue→Paid idempotente)
- `StudentEnrolledDomainEvent`, `StudentFinancialStatusChangedDomainEvent`

**Academic.Application**:
- `IStudentRepository` port (5 métodos, sin referencias a EF Core)
- `IUlidGenerator` port (ADR-036)
- `IOutboxEventReader` port (ADR-036, R10)
- `EnrollStudentCommand` (ICommand<EnrollStudentResponse>) + handler + validator
- 4 query handlers (GetStudents, GetStudentById, GetStudentStatus, GetStudentEvents)
- DTOs: StudentListItemDto, StudentDetailDto, GuardianDto, StudentStatusDto, StudentEventDto, PagedList<T>

**Academic.Infrastructure**:
- `AcademicDbContext` (BaseDbContext + 3 tablas outbox: AddOutboxMessageEntity/State/InboxState)
- `StudentConfiguration` (snake_case, OwnsOne Enrollment + Guardian, DocumentId VO conversion)
- `StudentRepository` (no SaveChanges — UoW maneja)
- `OutboxEventReader` (raw SQL sobre "OutboxMessage" tabla — columns verificados en migración)
- `UlidGenerator` (NUlid impl, Singleton)
- `AcademicDesignTimeDbContextFactory` (para CLI migrations)
- EF Migration `InitialAcademic` (students + OutboxMessage + OutboxState + InboxState)
- `DependencyInjection.AddAcademicInfrastructure` (MassTransit correcto: cfg→AddCampusConnectMassTransit<AcademicDbContext>)

**Academic.API**:
- `Program.cs`: JWT Bearer (fallback key) + AddAcademicApplication + AddAcademicInfrastructure + 2 policies + NpgSql health + `public partial class Program {}`
- `StudentEndpoints.cs`: 5 endpoints (POST/GET con políticas correctas)
- `appsettings.json`: ConnectionStrings:AcademicDb + Jwt + RabbitMQ

**Academic.Tests**:
- `AcademicWebApplicationFactory` (Testcontainers postgres:16-alpine + MassTransit TestHarness InMemory)
- `JwtTestHelper` (firma JWT con constantes appsettings dev)
- Unit tests: 31 tests (VOs, enums, Student aggregate, handlers)
- Integration tests: 19 tests (POST/GET endpoints + outbox DB verification)

### Contratos congelados (one-way door)

Los 3 records en `BuildingBlocks.Contracts/Events/` son **irreversibles** tras este PR.
Cualquier cambio de campos requiere un PR cross-cutting que actualice todos los consumers
(Payments, Attendance, Notifications, Analytics).

| Contrato | Campos exactos |
|---|---|
| `StudentEnrolled` | StudentId, EnrollmentId (ADR-034), SchoolId, Grade, FullName |
| `StudentStatusUpdated` | StudentId, AcademicStatus, FinancialStatus |
| `PaymentConfirmed` | PaymentId, ObligationId, StudentId, Amount (decimal), Method |

### Smoke e2e completado: 2026-06-17

```bash
# Enroll (Secretaria role)
POST http://localhost:8080/api/academic/students [Bearer: Secretaria JWT]
→ 201 { studentId (26 chars ULID), enrollmentId (26 chars ULID), status: "Active" }

# List
GET http://localhost:8080/api/academic/students [Bearer: Secretaria JWT]
→ 200 { items: [...], total: 1 }

# Detail
GET http://localhost:8080/api/academic/students/{id} [Bearer: Secretaria JWT]
→ 200 { studentId, fullName, documentId, grade, schoolId, academicStatus, financialStatus, guardian }

# Status (any authenticated user)
GET http://localhost:8080/api/academic/students/{id}/status [Bearer: any JWT]
→ 200 { studentId, exists: true, academicStatus: "Active", financialStatus: "Pending" }
GET ... [sin token] → 401

# Events (from OutboxMessage table)
GET http://localhost:8080/api/academic/students/{id}/events [Bearer: Secretaria JWT]
→ 200 { items: [{ eventType: "...StudentEnrolled...", occurredAt, correlationId }] }

# Auth enforcement
POST ... [sin token] → 401
POST ... [role: "Docente"] → 403
```

### Gotchas adicionales descubiertos durante apply

**Gotcha 23 — NUlid versión 1.7.3, no 3.x**: La spec asumió NUlid 3.x pero la versión disponible en nuget.org es 1.7.3. API: `Ulid.NewUlid(DateTimeOffset)` es la misma. No hay impacto funcional.

**Gotcha 24 — Domain no puede referenciar Application (Result<T> en VOs)**: `DocumentId` y `GuardianContact` en `Academic.Domain` necesitaban `Result<T>` de `BuildingBlocks.Application`, pero `Academic.Domain.csproj` sólo referencia `BuildingBlocks.Domain`. Solución: patrón `TryCreate()` que retorna `(T?, string?)` en lugar de `Result<T>`.

**Gotcha 25 — IQuery<T> ya wrappea en Result<T>**: Idéntico a Gotcha 19. `IQuery<StudentDetailDto>` = `IRequest<Result<StudentDetailDto>>`. Usar `IQuery<Result<StudentDetailDto>>` es double-wrap.

**Gotcha 26 — OutboxMessage tabla tiene nombre PascalCase**: MassTransit 8.x crea la tabla como `OutboxMessage` (PascalCase), NO `outbox_message` (snake_case). Columnas verificadas en migración generada: MessageType, Body, SentTime, CorrelationId. El OutboxEventReader usa raw SQL con comillas dobles para PostgreSQL.

**Gotcha 27 — MassTransit.TestHarness NO es paquete separado en 8.x**: `AddMassTransitTestHarness()` está en el paquete `MassTransit` base. No existe `MassTransit.TestHarness` como paquete NuGet independiente.

**Gotcha 28 — Outbox INSERT requiere IPublishEndpoint antes de SaveChanges**: Con MassTransit EF Core outbox + `UseBusOutbox()`, la llamada a `IPublishEndpoint.Publish<T>()` DEBE ocurrir ANTES de `SaveChangesAsync()`. Si se hace post-commit (via domain event dispatch en BaseDbContext), el mensaje se inserta en una transacción separada rompiendo la atomicidad. Solución: llamar `IPublishEndpoint.Publish<StudentEnrolled>()` directamente en el command handler, antes de retornar. UnitOfWorkBehavior luego llama SaveChangesAsync que commit el INSERT students + INSERT OutboxMessage en la misma TX.

**Gotcha 29 — ConnectionStrings__Default vs AcademicDb en docker-compose**: `docker-compose.yml` para `academic-service` tenía `ConnectionStrings__Default` pero el código usa `ConnectionStrings:AcademicDb`. Solución: añadir `ConnectionStrings__AcademicDb` en el environment del servicio.

### Próximo cambio SDD

`academic-service Phase 2` — PaymentConfirmedConsumer + StudentStatusUpdated publish + FinancialStatus.Overdue transition
