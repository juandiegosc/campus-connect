# CampusConnect 360 — Contexto del proyecto

## Descripción
Plataforma de gestión escolar con arquitectura de microservicios (.NET 10, Clean Architecture, MediatR, MassTransit, Ocelot).

## CampusConnect 360 — SDD Progress
- **Cambio actual**: `identity-service` (Phase 2) — PRÓXIMO
- **Sub-fase actual**: COMPLETO — todas las fases 1–10 de `backend-bootstrap` completadas
- **Próxima sub-fase**: N/A — cambio `backend-bootstrap` cerrado
- **Próximo cambio SDD**: `identity-service` (Phase 2) — ACTIVO
- **Última actualización**: 2026-06-14
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

## Estado del sistema (backend-bootstrap COMPLETO)

### Proyectos en solución
| Proyecto | Tipo | Ubicación |
|---|---|---|
| BuildingBlocks.Contracts | classlib | src/BuildingBlocks/BuildingBlocks.Contracts/ |
| BuildingBlocks.Domain | classlib | src/BuildingBlocks/BuildingBlocks.Domain/ |
| BuildingBlocks.Application | classlib | src/BuildingBlocks/BuildingBlocks.Application/ |
| BuildingBlocks.Infrastructure | classlib | src/BuildingBlocks/BuildingBlocks.Infrastructure/ |
| CampusConnect.Gateway | web | src/Gateway/CampusConnect.Gateway/ |
| Identity.API | web stub | src/Services/Identity/Identity.API/ |
| Academic.API | web stub | src/Services/Academic/Academic.API/ |
| Payments.API | web stub | src/Services/Payments/Payments.API/ |
| Attendance.API | web stub | src/Services/Attendance/Attendance.API/ |
| Notifications.API | web stub | src/Services/Notifications/Notifications.API/ |
| Analytics.API | web stub | src/Services/Analytics/Analytics.API/ |

## Convenciones
- Clean Architecture estricta: Domain → Application → Infrastructure → API
- CQRS lógico con MediatR + Result<T> (sin excepciones para errores de negocio)
- kebab-case en colas/exchanges de RabbitMQ
- CorrelationId propagado via X-Correlation-Id header en toda la cadena
- Minimal APIs + OpenAPI nativo (sin Swashbuckle)
- TreatWarningsAsErrors=true solo en src/BuildingBlocks/
- Ocelot: rutas /health siempre sin AuthenticationOptions; rutas de negocio con Bearer
