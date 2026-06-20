# CampusConnect 360 — Infraestructura y esqueleto de la solución

Describe el docker-compose, la estructura de carpetas y cómo arrancar el entorno. Acompaña a docs/01 y docs/02.

## 1. Topología de contenedores

| Contenedor | Imagen | Puerto host | Rol |
|---|---|---|---|
| cc-rabbitmq | rabbitmq:3.13-management | 5672 / 15672 | Bus de mensajería + panel |
| cc-postgres | postgres:16 | 5438 | **Una sola** instancia con TODAS las bases |
| cc-identity-service ... cc-analytics-service | build .NET | interno 8080 | Microservicios |
| cc-gateway | build .NET (Ocelot) | 8080 | Entrada única |

Una única instancia Postgres (`cc-postgres`) en `localhost:5438` aloja las seis bases lógicas — `identity_db`, `academic_db`, `payments_db`, `attendance_db`, `notifications_db`, `analytics_db` — creadas en la primera inicialización por `infra/postgres/init-databases.sql`. Sigue siendo base por servicio a nivel lógico (una connection string por servicio, sin tablas compartidas), pero consume mucha menos memoria que seis contenedores y se inspecciona desde un único `localhost:5438`.

## 2. Estructura de la solución

```
campus-connect/
├── docker-compose.yml
├── .env.example
├── build/
│   └── Dockerfile.template
├── docs/
│   ├── 01-planificacion-backend.md
│   ├── 02-contratos-api-eventos.md
│   └── 03-infraestructura-esqueleto.md
└── src/
    ├── BuildingBlocks/
    │   ├── BuildingBlocks.Contracts/
    │   ├── BuildingBlocks.Domain/
    │   ├── BuildingBlocks.Application/
    │   └── BuildingBlocks.Infrastructure/
    ├── Gateway/
    │   └── CampusConnect.Gateway/        (ocelot.json incluido)
    └── Services/
        ├── Identity/{API,Application,Domain,Infrastructure}
        ├── Academic/{API,Application,Domain,Infrastructure}
        ├── Payments/{API,Application,Domain,Infrastructure}
        ├── Attendance/{API,Application,Domain,Infrastructure}
        ├── Notifications/{API,Application,Domain,Infrastructure}
        └── Analytics/{API,Application,Domain,Infrastructure}
```

Las carpetas de capas están creadas con .gitkeep. Los proyectos .NET se generan en el siguiente paso.

## 3. Profiles de Docker Compose

El compose usa profiles para separar infraestructura de aplicación:

- Sin profile: solo arrancan rabbitmq y la base `cc-postgres` (con sus seis bases lógicas). Útil ahora, antes de tener código.
- Profile services: arranca además los microservicios y el gateway (requieren Dockerfile).

Arrancar solo infraestructura:
```
docker compose up -d
```

Arrancar todo (cuando existan los Dockerfile):
```
docker compose --profile services up -d --build
```

## 4. Puesta en marcha

1. Copiar el entorno: `cp .env.example .env` y ajustar JWT_SIGNING_KEY.
2. Levantar infraestructura: `docker compose up -d`.
3. Verificar RabbitMQ en http://localhost:15672 (guest/guest por defecto).
4. Verificar las bases: `docker exec -it cc-postgres psql -U campus -d academic_db -c "\l"` (lista las 6 bases lógicas).
5. Generar los proyectos .NET y sus Dockerfile (siguiente paso), luego `docker compose --profile services up -d --build`.

## 5. Dependencias de arranque

El compose usa depends_on con condition: service_healthy. Cada servicio espera a que su base y RabbitMQ estén sanos antes de iniciar, evitando fallos de conexión en el arranque. Los servicios .NET deben aplicar migraciones de EF Core al iniciar (o vía un init job) para crear su esquema y las tablas de outbox/inbox.

## 6. Gateway (Ocelot)

El archivo src/Gateway/CampusConnect.Gateway/ocelot.json define el ruteo por prefijo de servicio. Las rutas de negocio exigen autenticación (AuthenticationProviderKey Bearer); las de Identity (login, refresh) quedan abiertas. El gateway valida firma, emisor, audiencia y expiración del JWT antes de rutear.

## 7. Siguiente paso

Generar el esqueleto de proyectos .NET: crear el .sln, los cuatro proyectos del kernel, los cuatro proyectos por servicio con sus referencias de capa, el proyecto del gateway, y los Dockerfile a partir de build/Dockerfile.template. Después, implementar BuildingBlocks (Result, pipeline behaviors, configuración de MassTransit con outbox/inbox/DLQ) como base reutilizable.
