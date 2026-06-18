# CampusConnect 360 — Contexto del proyecto

## Descripción
Plataforma de gestión escolar con arquitectura de microservicios (.NET 10, Clean Architecture, MediatR, MassTransit, Ocelot).

## CampusConnect 360 — SDD Progress
- **Último cambio completado**: `payments-service-phase1` — LEAN bounded context: Obligation aggregate (posee Payment embebido 1:1) + register/confirm/GET endpoints + publish de PaymentConfirmed vía outbox (45/45 tasks, 74/74 tests verdes, verify passed-with-warnings 0 CRITICAL ✅). Cierra el loop e2e Payments→Academic.
- **Estado**: `backend-bootstrap` COMPLETO + `identity-service` COMPLETO + `academic-service` Phase 1 + Phase 2 COMPLETO + `payments-service` Phase 1 COMPLETO
- **Próximo cambio SDD**: `payments-service` Phase 2 (validación síncrona de StudentId vía Polly + StudentEnrolledConsumer + StudentReplica + GET /students) o `academic-service` Phase 3
- **Última actualización**: 2026-06-18
- **Contratos congelados (one-way door)**: StudentEnrolled (5 campos), StudentStatusUpdated (3 campos), PaymentConfirmed (5 campos) — NO modificar sin PR cross-cutting
- **BREAKING CHANGE Phase 3 Identity**: `POST /api/identity/users` ahora requiere `role=Direccion` JWT
- **JWT signing key convention**: `campus-connect-dev-placeholder-key-32b` (idéntico en Identity.API, Academic.API y Gateway — NUNCA cambiar uno sin el otro)
- **TODO multi-tenant**: `schoolId` hardcoded `"SCH-001"` en `JwtTokenService.cs` (Identity) y `Student.cs` (Academic) — buscar `// TODO multi-tenant`
- Ver `docs/PROGRESS.md` para detalle completo.

## Comandos rápidos
```bash
# Iniciar nueva fase SDD
/sdd-new identity-service

# Compilar toda la solución (11 proyectos)
dotnet build CampusConnect360.sln

# Restaurar paquetes
dotnet restore CampusConnect360.sln

# Arrancar infraestructura local (sin conflictos de puertos)
docker compose -f docker-compose.yml -f docker-compose.local.yml up -d

# Arrancar todos los servicios incluyendo Gateway
docker compose -f docker-compose.yml -f docker-compose.local.yml --profile services up -d

# Probar salud del sistema completo
curl http://localhost:8080/health
curl http://localhost:8080/api/identity/health
```

## Soluciones (.sln)

- `CampusConnect360.sln` (raíz): solución global con todos los proyectos.
- `src/Services/<Service>/<Service>.sln`: una solución por servicio para abrirla aislada en IDE.
  - Cada `.sln` por servicio incluye los proyectos del servicio + las 4 referencias de `BuildingBlocks.*` (carpeta de solución `BuildingBlocks`).
  - Identity además incluye `tests/Identity.Tests`.
  - Al añadir un nuevo proyecto a un servicio, añadirlo TAMBIÉN al `.sln` global y al `.sln` del servicio.

## Estado del sistema (backend-bootstrap COMPLETO)

### Proyectos en solución
| Proyecto | Tipo | Ubicación |
|---|---|---|
| BuildingBlocks.Contracts | classlib | src/BuildingBlocks/BuildingBlocks.Contracts/ |
| BuildingBlocks.Domain | classlib | src/BuildingBlocks/BuildingBlocks.Domain/ |
| BuildingBlocks.Application | classlib | src/BuildingBlocks/BuildingBlocks.Application/ |
| BuildingBlocks.Infrastructure | classlib | src/BuildingBlocks/BuildingBlocks.Infrastructure/ |
| CampusConnect.Gateway | web | src/Gateway/CampusConnect.Gateway/ |
| Identity.Domain | classlib | src/Services/Identity/Identity.Domain/ |
| Identity.Application | classlib | src/Services/Identity/Identity.Application/ |
| Identity.Infrastructure | classlib | src/Services/Identity/Identity.Infrastructure/ |
| Identity.API | web | src/Services/Identity/Identity.API/ |
| Identity.Tests | xunit.v3 | tests/Identity.Tests/ |
| Academic.Domain | classlib | src/Services/Academic/Academic.Domain/ |
| Academic.Application | classlib | src/Services/Academic/Academic.Application/ |
| Academic.Infrastructure | classlib | src/Services/Academic/Academic.Infrastructure/ |
| Academic.API | web | src/Services/Academic/Academic.API/ |
| Academic.Tests | xunit.v3 | tests/Academic.Tests/ |
| Payments.Domain | classlib | src/Services/Payments/Payments.Domain/ |
| Payments.Application | classlib | src/Services/Payments/Payments.Application/ |
| Payments.Infrastructure | classlib | src/Services/Payments/Payments.Infrastructure/ |
| Payments.API | web | src/Services/Payments/Payments.API/ |
| Payments.Tests | xunit.v3 | tests/Payments.Tests/ |
| Attendance.API | web stub | src/Services/Attendance/Attendance.API/ |
| Notifications.API | web stub | src/Services/Notifications/Notifications.API/ |
| Analytics.API | web stub | src/Services/Analytics/Analytics.API/ |

## Entorno de ejecución
- **Solo ejecución local** — este proyecto NO se despliega a entornos productivos.
- NO crear `docker-compose-prod.yml`, NO añadir gates `IsDevelopment()` ni medidas de hardening específicas de prod (HSTS, secret managers externos, rate-limiting agresivo, etc.).
- Endpoints administrativos (seed, debug, dev tools) pueden quedar públicos sin gating.
- Los secretos van en `.env` local; no se requiere integración con Azure Key Vault, AWS Secrets Manager o equivalentes.

## Alcance del proyecto
- **Proyecto académico (materia universitaria)** — NO es producto comercial ni cliente.
- **Apegarse ESTRICTAMENTE a las funcionalidades documentadas en `docs/`** (PROGRESS.md, specs SDD, ADRs). NO añadir features fuera del alcance de la fase actual.
- NO scope creep: si una mejora "obvia" o "nice-to-have" surge durante apply/verify y NO está en el spec de la fase, registrarla como follow-up para la fase siguiente — no implementarla.
- Cada fase SDD tiene un proposal/spec que define QUÉ entra. Cualquier ambigüedad se resuelve consultando docs primero, no extendiendo el alcance.
- Excepción única: bugs que rompan builds/tests existentes deben arreglarse en la fase actual.

## Convenciones
- Clean Architecture estricta: Domain → Application → Infrastructure → API
- CQRS lógico con MediatR + Result<T> (sin excepciones para errores de negocio)
- kebab-case en colas/exchanges de RabbitMQ
- CorrelationId propagado via X-Correlation-Id header en toda la cadena
- Minimal APIs + OpenAPI nativo (sin Swashbuckle)
- TreatWarningsAsErrors=true solo en src/BuildingBlocks/
- Ocelot: rutas /health siempre sin AuthenticationOptions; rutas de negocio con Bearer
