# CampusConnect 360 — Contexto del proyecto

## Descripción
Plataforma de gestión escolar con arquitectura de microservicios (.NET 10, Clean Architecture, MediatR, MassTransit, Ocelot).

## CampusConnect 360 — SDD Progress
- **Cambio actual**: `backend-bootstrap` (Phase 1)
- **Sub-fase actual**: Fases 1–6 de 10 completadas (kernel + tooling)
- **Próxima sub-fase**: Fase 7 (Gateway) + Fase 8 (stubs) + Fase 9 (verificación e2e) + Fase 10 (cierre)
- **Próximo cambio SDD**: `identity-service` (Phase 2)
- **Última actualización**: 2026-06-14
- Ver `docs/PROGRESS.md` para detalle completo.

## Comandos rápidos
```bash
# Retomar apply del cambio en curso
/sdd-apply backend-bootstrap

# Compilar kernel
dotnet build CampusConnect360.sln

# Restaurar paquetes
dotnet restore CampusConnect360.sln
```

## Convenciones
- Clean Architecture estricta: Domain → Application → Infrastructure → API
- CQRS lógico con MediatR + Result<T> (sin excepciones para errores de negocio)
- kebab-case en colas/exchanges de RabbitMQ
- CorrelationId propagado via X-Correlation-Id header en toda la cadena
- Minimal APIs + OpenAPI nativo (sin Swashbuckle)
- TreatWarningsAsErrors=true solo en src/BuildingBlocks/
