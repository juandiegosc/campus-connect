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

---

## Fase 5 — academic-service Phase 2 (PaymentConfirmed Consumer + StudentStatusUpdated Publish)

**Cambio**: `academic-service-phase2`
**Estado**: COMPLETO — 28/28 tasks completadas
**Fecha**: 2026-06-18
**Tests**: 62/64 verdes (50 baseline + 14 nuevos) + 2 skips intencionales (ESC-60, ESC-63)
**Verify**: passed-with-warnings — 0 CRITICAL, 2 WARNING, 2 SUGGESTION
**ADRs**: ADR-039..043 finalizados

### Alcance entregado

**Academic.Application**:
- `IStudentRepository.UpdateAsync` — nuevo port para persistir mutación de estado
- `ConfirmPayment/ConfirmStudentPaymentCommand` (ICommand) — reacciona a PaymentConfirmed
- `ConfirmPayment/ConfirmStudentPaymentCommandHandler` — carga Student, `ConfirmPayment()` idempotente (Pending/Overdue→Paid), publica `StudentStatusUpdated` ANTES de SaveChangesAsync (atomicidad)

**Academic.Infrastructure**:
- `Messaging/Consumers/PaymentConfirmedConsumer` — consumer thin (sin lógica de negocio), despacha el command
- `DependencyInjection.cs` — `AddConsumer<PaymentConfirmedConsumer>()` en DI de producción (ADR-042)
- `StudentRepository.UpdateAsync` (sin SaveChanges — UoW maneja)

**Academic.Tests**:
- `AcademicWebApplicationFactory` — `AddConsumer<PaymentConfirmedConsumer>()` en el TestHarness (ADR-042, segundo call site obligatorio)
- 8 unit tests `ConfirmStudentPaymentCommandHandlerTests`
- 4 integration tests activos + 2 skipped `PaymentConfirmedConsumerIntegrationTests`

### Decisiones (ADR-039..043)
- **ADR-042**: el consumer DEBE registrarse en AMBOS sitios (DI producción + TestHarness). Si falta en el harness, InboxState se bypassa silenciosamente.
- **ADR-043**: estrategia de fallback para CorrelationId null en el consumer.

### Skips justificados
- **ESC-60** (dedup por MessageId): MassTransit 8.3.6 InMemory TestHarness no expone API pública confirmada para forzar MessageId. Invariante documentada en el cuerpo del test; ESC-59 cubre idempotencia a nivel dominio.
- **ESC-63** (atomicidad bajo fallo de SaveChanges): requiere interceptor de SaveChanges no presente. ESC-58 cubre el caso positivo de atomicidad.

### Carry-forward a la siguiente fase
- **WARNING-1**: handler usa `DateTime.UtcNow` directo en vez de `TimeProvider` inyectado — convención heredada de Phase 1 (EnrollStudentCommandHandler), sin regresión funcional.
- **SUGGESTION-2**: extraer fakes compartidos a un archivo `Fakes/` para eliminar el workaround de sufijo "2" (FakeStudentRepository2, etc.).
- **Gap de cobertura**: ESC-60/ESC-63 quedan sin test ejecutable.

### Contratos congelados (sin cambios)
`PaymentConfirmed` (5 campos consumidos), `StudentStatusUpdated` (3 campos publicados), `StudentEnrolled` (5 campos) — intactos en BuildingBlocks.Contracts.

### Próximo cambio SDD

`payments-service Phase 1` — publisher de `PaymentConfirmed` para cerrar el flujo e2e Payments→Academic, o `academic-service Phase 3`.

---

## Fase 6 — payments-service Phase 1 (LEAN: Obligation + Confirm + PaymentConfirmed publish)

**Cambio**: `payments-service-phase1`
**Estado**: COMPLETO — 45/45 tasks completadas
**Fecha**: 2026-06-18
**Build**: `dotnet build CampusConnect360.sln` → 0 errores
**Tests**: 74/74 verdes (46 unit + 28 integration, Testcontainers postgres:16-alpine)
**Verify**: passed-with-warnings — 0 CRITICAL, 2 WARNING, 1 SUGGESTION
**Delivery**: single-PR con size:exception (~420-460 LOC, bounded context greenfield completo)
**ADRs**: ADR-044..051

### Alcance LEAN entregado (decisión explícita del usuario)
Nuevo bounded context Payments con 4 capas. Publica el contrato congelado `PaymentConfirmed`, cerrando el loop e2e Payments→Academic (Academic Phase 2 ya lo consume).

**Payments.Domain**:
- `ObligationId` / `PaymentId` VOs (ULID via NUlid 1.7.3)
- `ObligationStatus` (Pending/Confirmed) + `PaymentMethod` enums
- `Obligation` aggregate que **posee** `Payment` embebido (1:1, confirmado in-place) — ADR-044
- `Obligation.ConfirmPayment()` es `void`; idempotencia a nivel handler (ADR / Gotcha 24)
- `PaymentConfirmedDomainEvent`

**Payments.Application**:
- `IObligationRepository` + `IUlidGenerator` ports
- `RegisterObligationCommand` + `ConfirmPaymentCommand` (ICommand<T>) + handlers + validators
- `GetObligations` / `GetObligationById` queries (single-wrap `IQuery<Dto>`)
- DTOs: ObligationListItemDto, ObligationDetailDto, PaymentDto
- `ConfirmPaymentCommandHandler` publica `PaymentConfirmed` ANTES de SaveChangesAsync (Gotcha 28)

**Payments.Infrastructure**:
- `PaymentsDbContext` (BaseDbContext + OutboxMessage + OutboxState; SIN InboxState en scope — ADR-046)
- `ObligationConfiguration` (snake_case, OwnsOne Payment, conversiones ULID/enum)
- `ObligationRepository` (sin SaveChanges — UoW)
- `UlidGenerator` + design-time factory + migración `InitialPayments`

**Payments.API**:
- `Program.cs`: JWT Bearer + policy `Finanzas` + Npgsql health + dual health + public partial Program
- `PaymentEndpoints`: POST obligations, POST obligations/{id}/confirm, GET obligations, GET obligations/{id}
- `appsettings.json`: ConnectionStrings:PaymentsDb + Jwt + RabbitMQ

**Payments.Tests**:
- `PaymentsWebApplicationFactory` (Testcontainers + MassTransit TestHarness InMemory) + `JwtTestHelper` (Finanzas)
- 46 unit + 28 integration tests

### Decisiones LEAN (diferido a Phase 2 — ADR-045/047)
- StudentId **confiado** (solo format check 26 chars), SIN validación cross-service.
- SIN `StudentEnrolledConsumer` / SIN `StudentReplica` / SIN llamada síncrona Polly a Academic.
- SIN `GET /api/payments/students`. SIN multi-tenant schoolId.

### Bug pre-existente corregido (ADR-048)
`docker-compose.yml` del payments-service usaba `ConnectionStrings__Default` + `RabbitMq__Host/User/Pass` (claves que el código ignora → fallo silencioso de RabbitMQ y DB). Corregido a `ConnectionStrings__PaymentsDb` + `RABBITMQ_HOST/USER/PASS`. Eliminado también `Services__Academic__BaseUrl` (config muerta en scope lean) y el residual `ConnectionStrings__Default` (ESC-PM-28).

### Desviaciones aceptadas
- **InboxState en migración**: MassTransit 8.x `AddEntityFrameworkOutbox<T>` añade la tabla InboxState incondicionalmente aunque `OnModelCreating` NO llame `AddInboxStateEntity()`. Inofensiva (sin uso en Phase 1), idéntica a Academic Phase 1.

### Gotcha nuevo descubierto
**Gotcha 30 — `harness.Published` NO verifica publishes vía outbox**: con `UseBusOutbox()` el relay es asíncrono y el test termina antes de que el mensaje se entregue. Para verificar que `PaymentConfirmed` se escribió, usar **raw SQL sobre la tabla `OutboxMessage`** (patrón de Academic EnrollStudent). El uso de `harness.Published` en Academic Phase 2 aplicaba al lado **consumer**, no al publish vía outbox.

### Carry-forward a Payments Phase 2
- Validación síncrona de existencia de StudentId (Polly + circuit breaker contra Academic `GET /students/{id}/status`).
- `StudentEnrolledConsumer` + `StudentReplica` + uso real de InboxState.
- `GET /api/payments/students`. Multi-tenant schoolId.
- SUGGESTION-1: test de atomicidad transaccional con rollback a nivel integración.

### Estado del loop e2e
Payments publica `PaymentConfirmed` → Academic Phase 2 lo consume → transición FinancialStatus + publish StudentStatusUpdated. **El loop e2e es ahora cerrable**. Nota: el smoke test con docker compose vivo NO se ejecutó en esta sesión.

### Próximo cambio SDD

`payments-service Phase 2` (validación síncrona + consumer + réplica) o `academic-service Phase 3`.

---

## Fase 7 — payments-service Phase 2 (async StudentReplica + validación StudentId + GET /students)

**Cambio**: `payments-service-phase2`
**Estado**: COMPLETO — 34/34 tasks completadas
**Fecha**: 2026-06-18
**Build**: `dotnet build` → 0 errores
**Tests**: 94/94 verdes (50 de Phase 1 + 44 nuevos/modificados)
**Verify**: passed-with-warnings — 0 CRITICAL, 2 WARNING, 2 SUGGESTION
**ADRs**: ADR-052..059

### Decisión arquitectónica central (ADR-052): Opción B — réplica asíncrona, SIN Polly
Se descartó la validación síncrona HTTP con Polly/circuit-breaker a favor de un read model local poblado por eventos. **Cero acoplamiento en runtime** con Academic (si Academic cae, Finanzas sigue operando). Tradeoff aceptado: consistencia eventual (ventana ms-segundos, irrelevante en flujo escolar real).

### Alcance entregado
**Payments.Infrastructure**:
- `Messaging/Consumers/StudentEnrolledConsumer` — consume `StudentEnrolled` (frozen 5 campos) → upsert de réplica. Thin. ADR-043 CorrelationId null fallback.
- `Persistence/ReadModels/StudentReplica` — read model (NO aggregate de dominio, sin clases base de Domain) — ADR-054
- `StudentReplicaConfiguration` (tabla `student_replicas`, snake_case, student_id PK)
- `StudentReplicaRepository` — UPSERT load-or-create (FindAsync→update/insert) ADR-058; **llama SaveChangesAsync explícito** (ADR-057, el consumer no pasa por UnitOfWorkBehavior; atómico con InboxState vía la transacción del inbox de MassTransit)
- `PaymentsDbContext` — descomentado `AddInboxStateEntity()` (InboxState ahora ACTIVO; estaba dormante desde Phase 1) + DbSet<StudentReplica>
- Migración `AddStudentReplicas` — solo tabla student_replicas (InboxState ya existía en InitialPayments)
- `DependencyInjection` — `AddConsumer<StudentEnrolledConsumer>()` antes de AddCampusConnectMassTransit (ADR-042)

**Payments.Application**:
- `IStudentReplicaRepository` port (primitivos/DTOs, sin fuga de POCO/IQueryable) — Upsert/Exists/GetPaged
- `GetStudentsQuery` (IQuery<PagedList<StudentReplicaItemDto>> single-wrap) + handler + DTOs
- `RegisterObligationCommandHandler` — guarda de existencia: StudentId desconocido → `Error.Validation("student.not_found")` → **HTTP 400** (ADR-056, NO 404)

**Payments.API**:
- `StudentEndpoints` — `GET /api/payments/students` (Finanzas, paginado page/pageSize + grade/search) + registro en Program.cs

**Payments.Tests**:
- `StudentEnrolledConsumerTests` (raw SQL sobre student_replicas + harness Consumed, Gotcha 30; idempotencia; upsert on conflict)
- `GetStudentsIntegrationTests` (paginación/filtro/auth) + `GetStudentsQueryHandlerTests`
- 22 tests pre-existentes de obligations ajustados (ahora pre-siembran réplica porque RegisterObligation valida existencia)

### ADR-042 dual registration (gotcha recurrente)
`AddConsumer<StudentEnrolledConsumer>()` en AMBOS sitios: `DependencyInjection.cs` (producción) Y `PaymentsWebApplicationFactory.cs` (test harness). Omitir uno rompe prod o tests en silencio.

### Desviaciones aceptadas / warnings
- **W1**: el test de idempotencia ESC-PM-32 no aísla específicamente el dedup de InboxState (el UPSERT también garantiza fila única). Gap de cobertura, no bug → test dedicado en Phase 3.
- **W2**: `MapError` ahora prefija `[error.code]` en el `detail` de TODOS los errores de obligation (no solo student.not_found). Inofensivo en local → follow-up Phase 3: campo `extensions.code` RFC 7807 propio.
- `Task.Delay(800ms)` en consumer tests para esperar el commit de la TX del outbox — posible flakiness en CI lento.

### Carry-forward a Payments Phase 3
- Consumir `StudentStatusUpdated` para mantener AcademicStatus actualizado en la réplica.
- Ciclo de vida de la réplica (delete/deactivate).
- Campo `extensions.code` RFC 7807 a nivel infra.
- `schoolId` multi-tenant (sigue siendo cross-cutting, cambio aparte).
- Test dedicado de dedup InboxState (W1).

### Estado del event mesh
**Bidireccional**: Payments publica `PaymentConfirmed` → Academic lo consume (Academic Phase 2); Academic publica `StudentEnrolled` → Payments lo consume (esta fase). Nota: smoke e2e con docker compose vivo NO ejecutado en esta sesión.

### Próximo cambio SDD

`payments-service Phase 3` (StudentStatusUpdated consumer) o `academic-service Phase 3`.

---

## Fase 8 — payments-service Phase 3 (StudentStatusUpdated consumer + replica status sync)

**Cambio**: `payments-service-phase3`
**Estado**: COMPLETO
**Fecha**: 2026-06-19
**Build**: limpio (solo warnings xUnit1051 informativos)
**Tests**: 100/100 verdes (94 baseline Phase 2 + 6 nuevos ESC-PM-51..56)
**ADRs**: ADR-060..062

> Nota de ejecución: spec/design/apply se hicieron INLINE por el orquestador — el límite de cuenta de sub-agentes se alcanzó (4:20pm) durante la delegación de spec/design. El trail SDD completo igual quedó en engram (spec #211, design #212, apply-progress/archive #213).

### Alcance entregado
**Payments.Infrastructure**:
- `Messaging/Consumers/StudentStatusUpdatedConsumer` — consume `StudentStatusUpdated` (frozen 3 campos) → `UpdateStatusAsync`. Thin. ADR-043 CorrelationId fallback.
- `StudentReplica` + config: nuevas columnas nullable `academic_status` + `financial_status` (varchar(50), enum names de Academic verbatim — ADR-061)
- `StudentReplicaRepository.UpdateStatusAsync` — **no-op + WARNING si la fila no existe** (ADR-060, nunca crea fila fantasma); SaveChanges explícito (ADR-057); inyecta ILogger
- Migración `AddStatusColumnsToStudentReplicas` — solo las 2 columnas (sin drift de InboxState)

**Payments.Application**:
- `IStudentReplicaRepository.UpdateStatusAsync` (primitivos, ADR-054)
- `StudentReplicaItemDto` + 2 campos nullable con default (`= null`) → no rompe sitios de 5 args; `GetPagedAsync` proyecta los 2 nuevos campos

**Payments.API**: `GET /api/payments/students` ahora expone `academicStatus` + `financialStatus` (null para réplicas sin evento de status aún) — additivo, no-breaking.

**Tests**: `StudentStatusUpdatedConsumerTests` (ESC-PM-51..54, helper raw SQL con columnas de status); `GetStudentsIntegrationTests` extendido (ESC-PM-55/56); fake actualizado con UpdateStatusAsync.

### Decisión central (ADR-060): missing replica = no-op + WARNING
`StudentStatusUpdated` es un overlay secundario sobre una réplica que `StudentEnrolled` posee. Si llega para un StudentId aún no replicado → log WARNING + return (sin throw, sin fault, sin fila fantasma). Problema de orden transitorio aceptable en local.

### Carry-forward a Phase 4 (si aplica)
- Ciclo de vida de réplica (delete/deactivate ante StudentWithdrawn).
- `schoolId` multi-tenant (cross-cutting).
- Campo `extensions.code` RFC 7807 (W2 de Phase 2, aún diferido).

### Próximo cambio SDD
`academic-service Phase 3` o consolidar otro bounded context (Attendance/Notifications).

---

## Fase 9 — academic-service Phase 3 (MarkOverdue: FinancialStatus.Overdue transition)

**Cambio**: `academic-service-phase3`
**Estado**: COMPLETO
**Fecha**: 2026-06-19
**Tests**: 72/74 verdes (2 skips pre-existentes ESC-60/ESC-63; +10 nuevos: 3 dominio, 4 handler, 3 integración)
**ADRs**: ADR-063, ADR-064

> Nota de ejecución: ciclo hecho INLINE por el orquestador (límite de cuenta de sub-agentes activo). Trail en engram: proposal #215, apply-progress/archive #216.

### Alcance (elegido por el usuario): solo MORA FINANCIERA
Operación `MarkOverdue` que transiciona `FinancialStatus` → Overdue y publica `StudentStatusUpdated` (congelado) vía outbox. (La opción de ciclo de vida académico Suspend/Graduate quedó descartada para esta fase.)

### Alcance entregado
**Academic.Domain** — `Student.MarkOverdue(nowUtc)`:
- Pending → Overdue (raise StudentFinancialStatusChangedDomainEvent)
- Overdue → no-op idempotente (sin evento)
- Paid → DomainException (defensa; el handler lo guarda con 409 antes)

**Academic.Application** — `MarkOverdue/`:
- `MarkStudentOverdueCommand(string StudentId) : ICommand` (HTTP-triggered, sin CorrelationId — espeja EnrollStudent) + validator (NotEmpty + Length 26)
- Handler: load→404; si Paid → Result.Failure(Conflict "student.already_paid"); MarkOverdue; UpdateAsync; publish StudentStatusUpdated ANTES de SaveChanges (Gotcha 28)

**Academic.API**:
- `POST /api/academic/students/{id}/mark-overdue` — policy `SecretariaOrDireccion` (ADR-064, sin rol Finanzas nuevo en Academic). 200 / 404 / 409 / 401 / 403.

**Academic.Tests**: 3 dominio (StudentDomainTests), 4 handler (reusa FakeStudentRepository2/FakeIntegrationEventPublisher2 del mismo namespace), 3 integración (happy 200+Overdue+fila outbox, 401, 403).

### Decisiones
- **ADR-063**: semántica MarkOverdue (Pending→Overdue, Overdue idempotente, Paid→409 Conflict).
- **ADR-064**: endpoint bajo policy existente SecretariaOrDireccion (sin scope creep de roles).

### Gotcha confirmado (refina Gotcha 30)
Para publishes vía outbox en path **HTTP** (no consumer): assertar sobre la tabla `"OutboxMessage"` por raw SQL (MessageType LIKE %StudentStatusUpdated% AND Body LIKE %studentId%), NO `harness.Published` — el path HTTP stagea pero no entrega prontamente al bus in-memory del harness (a diferencia del path consumer que espera `Consumed`). Espeja el patrón de EnrollStudent.

### Carry-forward
- Ciclo de vida académico (Suspend/Reactivate/Graduate sobre AcademicStatus) — opción descartada de esta fase, candidata a fase futura.
- Barrido masivo/programado de mora (job sobre fechas de vencimiento). schoolId multi-tenant.

### Próximo cambio SDD
academic-lifecycle o consolidar Attendance/Notifications/Analytics.

---

## Fase 10 — academic-service Phase 4 (ciclo de vida académico: Suspend/Reactivate/Graduate)

**Cambio**: `academic-service-phase4`
**Estado**: COMPLETO
**Fecha**: 2026-06-19
**Tests**: 104/106 verdes (+32 nuevos; 2 skips pre-existentes ESC-60/ESC-63)
**Verify**: PASS — 0 CRITICAL, 0 WARNING, 1 SUGGESTION (cosmético, ya corregido)
**ADRs**: ADR-065..068

> Desarrollado y commiteado directamente en `main` (directiva del usuario: sin ramas feature). Trail SDD en engram: explore #218, proposal #219, spec #220, design #221, tasks #222, apply #223, verify #224, archive #225.

### Alcance entregado
Completa el eje `AcademicStatus` (antes solo Active). 3 operaciones de administración que publican `StudentStatusUpdated` (congelado) vía outbox:

**Academic.Domain** — `Student.Suspend/Reactivate/Graduate(nowUtc)`:
- Suspend: Active→Suspended; ya-Suspended no-op idempotente; Graduated→DomainException
- Reactivate: Suspended→Active; ya-Active no-op idempotente; Graduated→DomainException
- Graduate: Active|Suspended→Graduated; Graduated→DomainException (terminal)
- **NO levantan domain event** (ADR-068, asimetría deliberada con MarkOverdue: el evento financiero no tiene consumidores internos; el handler publica StudentStatusUpdated directo)

**Academic.Application** — 3 folders (SuspendStudent/ReactivateStudent/GraduateStudent): Command(ICommand) + Validator (StudentId NotEmpty+Length 26) + Handler (load→404; guard Graduated→409 antes del throw de dominio; mutate; UpdateAsync; publish StudentStatusUpdated con AcademicStatus NUEVO + FinancialStatus actual ANTES de SaveChanges).

**Academic.API**:
- `POST /api/academic/students/{id}/suspend` + `/reactivate` → `SecretariaOrDireccion`
- `POST /api/academic/students/{id}/graduate` → `Direccion` (ADR-067)
- Registrada policy `Direccion` en Program.cs (no existía — dependencia dura de arranque)

**Academic.Tests**: 9 dominio (ESC-90..96) + ~11 handler + ~12 integración (happy 200 + academic_status raw SQL + fila OutboxMessage raw SQL; 401; 403; Graduate Secretaria→403). Reusa FakeStudentRepository2/FakeIntegrationEventPublisher2.

### Decisiones (ADR-065..068)
- **ADR-065**: 3 endpoints explícitos (no parametrizado).
- **ADR-066**: Graduate ya-Graduated → 409 (terminal, no idempotente como los otros).
- **ADR-067**: Graduate Direccion-only + registrar policy Direccion faltante.
- **ADR-068**: sin domain event de academic-status (el handler publica StudentStatusUpdated directo).

### Sin migración / sin cambio de contrato
`academic_status` ya estaba mapeado (HasConversion<string>()). `StudentStatusUpdated` (congelado) sin cambios — el campo AcademicStatus ahora lleva valores ≠ Active en runtime por primera vez.

### Downstream (sin cambio de código)
La réplica de Payments (`student_replicas.academic_status`) se actualiza transparentemente vía su `StudentStatusUpdatedConsumer` (Phase 3). Suspender/graduar ahora propaga el estado a Payments.

### Carry-forward
- Barrido masivo/programado de cambios de estado. schoolId multi-tenant.
- Bounded contexts pendientes: Attendance, Notifications, Analytics (stubs sin dominio).

### Próximo cambio SDD
Consolidar Attendance/Notifications/Analytics (greenfield) — son stubs.

---

## Fase 11 — attendance-service Phase 1 (bounded context completo: asistencia + incidentes + réplica)

**Cambio**: `attendance-service-phase1`
**Estado**: COMPLETO
**Fecha**: 2026-06-19
**Tests**: 56/56 verdes (25 unit + 31 integración). Solución completa compila (0 errores).
**Verify**: passed-with-warnings — 0 CRITICAL, 1 WARNING, 2 SUGGESTION
**ADRs**: ADR-069..075
**Alcance**: COMPLETO (con réplica) — elegido por el usuario

> Desarrollado y commiteado directamente en `main`. Trail SDD: explore #227, proposal #228, spec #230, design #229, tasks #231, apply #232, verify #234, archive #235.

### Contratos congelados POBLADOS (one-way door, ADR-070)
Estaban vacíos; ahora LOCKED:
- `AttendanceRecorded`: RecordId, StudentId, Date (ISO string), Status (Present|Absent|Late)
- `IncidentReported`: IncidentId, StudentId, Type, Severity (Low|Medium|High)
- `description` se guarda en la entidad Incident pero NO se publica.

### Alcance entregado (mirror Payments P1+P2)
**Attendance.Domain** (ADR-069 — 2 agregados independientes, sin relación padre-hijo):
- `AttendanceRecord` (AttendanceRecordId ULID VO, StudentId, Date DateOnly, AttendanceStatus enum, RecordedAt) → AttendanceRecordedDomainEvent
- `Incident` (IncidentId ULID VO, StudentId, Type, IncidentSeverity enum, Description, ReportedAt) → IncidentReportedDomainEvent

**Attendance.Application**: RecordAttendance + ReportIncident commands (guard StudentId existe en réplica → 400 student.not_found; publish ANTES de SaveChanges, ADR-075/Gotcha 28) + GetStudents + GetStudentHistory queries (IQuery single-wrap) + ports + validators.

**Attendance.Infrastructure**: AttendanceDbContext (Outbox+OutboxState+InboxState), configs snake_case, repos, `StudentEnrolledConsumer` (réplica), UlidGenerator, design-time factory, migración `InitialAttendance` (6 tablas: attendance_records, incidents, student_replicas, OutboxMessage, OutboxState, InboxState). ADR-042 dual registration (DI + WAF).

**Attendance.API**: Program.cs (JWT + policies Docente/DocenteOrDireccion + health + public partial Program) + 4 endpoints:
- `POST /api/attendance/records` (Docente)
- `POST /api/attendance/incidents` (Docente)
- `GET /api/attendance/students` (Docente)
- `GET /api/attendance/students/{id}/history` (DocenteOrDireccion)

**Attendance.Tests**: AttendanceWebApplicationFactory (Testcontainers + TestHarness), JwtTestHelper, 25 unit + 31 integración (outbox vía raw SQL sobre "OutboxMessage").

### Decisiones nuevas
- **ADR-074**: `Date` como `DateOnly` mapeado nativo en Npgsql 10 (sin converter); handler parsea ISO string → 400 si malformado; evento publica "yyyy-MM-dd".
- **ADR-075**: los agregados levantan domain event (intención) pero el HANDLER publica el integration event inline ANTES de SaveChanges (atomicidad — el dispatch post-save rompería la TX del outbox).

### Bug pre-existente corregido (ADR-072)
docker-compose attendance-service usaba `RabbitMq__Host/User/Pass` (ignoradas) → corregido a `RABBITMQ_HOST/USER/PASS` + `ConnectionStrings__AttendanceDb`. (Notifications y Analytics tienen el mismo bug — pendiente cuando se implementen.)

### Desviación aceptada / warnings
- **WARNING REQ-AT1-31**: el upsert de StudentReplica usa EF FindAsync+branch (consistente con Payments ADR-058 load-or-create, seguro bajo dedup de InboxState) en vez de raw `ON CONFLICT` SQL. No bloqueante en local; follow-up futuro.
- SUGGESTIONS: Task.Delay(800ms) en test de consumer (frágil en CI lento); 285 warnings xUnit1051 pre-existentes en Payments.Tests.

### Carry-forward
- Lifecycle de corrección/borrado de asistencia; paginación en history; upsert ON CONFLICT; consumir StudentStatusUpdated para reflejar estado académico en la réplica de Attendance. schoolId multi-tenant.

### Estado del sistema
Los 6 servicios de negocio con dominio: Identity, Academic (P1-4), Payments (P1-3), **Attendance (P1)**. Pendientes: **Notifications, Analytics** (stubs).

### Próximo cambio SDD
Notifications o Analytics (greenfield).

---

## Fase 12 — messaging-endpoint-isolation (fix de colisión de colas RabbitMQ)

**Cambio**: `messaging-endpoint-isolation`
**Estado**: COMPLETO
**Fecha**: 2026-06-20
**Tests**: Attendance 57/57, Payments 102/102, Academic 104/106 (2 skips previos). 3 tests nuevos (Strict TDD RED→GREEN).
**Verify**: PASSED — 0 CRITICAL. Verificación viva contra RabbitMQ real.
**ADR**: ADR-076.

### Problema (verificado en vivo)
Con Payments y Attendance corriendo a la vez contra el mismo RabbitMQ, la cola `StudentEnrolled` aparecía con **2 consumers** sobre UNA sola cola. Ambos servicios tienen un `StudentEnrolledConsumer` y, con el `DefaultEndpointNameFormatter` (sin prefijo), colisionaban en el mismo nombre de cola → *competing consumers*: cada evento `StudentEnrolled` llegaba a UN solo servicio → ambas réplicas de estudiantes quedaban incompletas. Los tests no lo detectaban (harness in-memory por proceso, nunca los dos servicios contra el mismo broker real).

### Decisión (ADR-076)
`AddCampusConnectMassTransit<TContext>` recibe un nuevo parámetro `serviceName` y configura `cfg.SetEndpointNameFormatter(new KebabCaseEndpointNameFormatter(prefix: serviceName, includeNamespace: false))`. Cada servicio prefija sus colas: `academic`, `payments`, `attendance`.

### Resultado (colas tras el fix)
`academic-payment-confirmed`, `payments-student-enrolled`, `payments-student-status-updated`, `attendance-student-enrolled` — 4 colas distintas, 1 consumer cada una. `StudentEnrolled` ahora se enruta a `payments-student-enrolled` Y `attendance-student-enrolled` (fan-out real). Verificado vía API de management (:15672).

### Archivos
- `BuildingBlocks.Infrastructure/Messaging/MassTransitExtensions.cs` (param `serviceName` + formatter)
- `Academic/Payments/Attendance .Infrastructure/DependencyInjection.cs` (pasan su prefijo)
- `tests/Attendance.Tests/Unit/EndpointNamingTests.cs`, `tests/Payments.Tests/Unit/EndpointNamingTests.cs` (nuevos)

> Trail SDD en engram: plan #242, verify-report #243. Commiteado directamente en `main`.
