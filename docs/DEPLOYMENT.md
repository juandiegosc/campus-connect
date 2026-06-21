# Cómo levantar CampusConnect 360 (local)

Guía paso a paso para correr todo el sistema con Docker. Proyecto **solo local** (no hay despliegue productivo).

---

## ⚠️ Lo más importante (leer primero)

El **Gateway y todos los microservicios están detrás del perfil `services`** de Docker Compose. Si corres `docker compose up` **sin** `--profile services`, **solo se levantan las bases de datos y RabbitMQ** — el Gateway NO aparece. Este es el error #1.

✅ Para levantar **todo** (incluido el Gateway), SIEMPRE usa `--profile services`:

```bash
docker compose -f docker-compose.yml -f docker-compose.local.yml --profile services up -d --build
```

---

## Requisitos

- **Docker** + **Docker Compose v2+** (probado con Docker 29 / Compose v5).
- (Opcional, solo si vas a compilar/desarrollar fuera de Docker) **.NET 10 SDK**.
- Puertos libres en el host: **8080** (Gateway). Postgres/RabbitMQ corren en la red interna (ver nota de conflictos abajo).

---

## Paso 1 — (Opcional) Variables de entorno

El sistema **funciona sin `.env`**: el compose ya trae valores por defecto para credenciales de DB, RabbitMQ y la clave JWT de desarrollo.

Si quieres personalizar (recomendado para que todos los servicios compartan una clave JWT explícita):

```bash
cp .env.example .env
# edita .env y define JWT_SIGNING_KEY con un valor de al menos 32 bytes
```

> `.env` está en `.gitignore` — no se commitea.

---

## Paso 2 — Elegir modo de ejecución

| Modo | Cuándo usarlo | Enlace |
|---|---|---|
| **A — Solo infra + `dotnet run`** ⭐ | Desarrollo activo, debugger, iteración rápida | [Ver Modo A](#modo-a--solo-infraestructura--dotnet-run-) |
| **B — Stack completo en Docker** | Validar build final, demo, CI | [Ver Modo B](#modo-b--stack-completo-en-docker) |

---

## Paso 3 — Usuarios de arranque (sembrados automáticamente)

Al primer arranque con una base vacía, Identity siembra **4 usuarios predeterminados** (uno por rol) de forma automática e idempotente — no se necesita SQL manual.

| Usuario | Contraseña | Rol |
|---|---|---|
| `director1` | `Admin1234!` | Direccion |
| `secretaria1` | `Admin1234!` | Secretaria |
| `finanzas1` | `Admin1234!` | Finanzas |
| `docente1` | `Admin1234!` | Docente |

> El seed corre en `IdentityDbInitializer.Seed` — si la tabla `users` ya tiene filas, no hace nada.

Haz login con cualquiera de ellos y usa el `accessToken` en las siguientes llamadas:

```bash
curl -X POST http://localhost:8080/api/identity/auth/login \
  -H 'Content-Type: application/json' \
  -d '{"username":"director1","password":"Admin1234!"}'
# → { accessToken, refreshToken, ... }

# Crear otro usuario (requiere rol Direccion):
curl -X POST http://localhost:8080/api/identity/users \
  -H 'Content-Type: application/json' \
  -H "Authorization: Bearer <accessToken>" \
  -d '{"username":"docente2","fullName":"Docente Dos","password":"Admin1234!","role":"Docente"}'

# Llamada protegida de otro servicio:
curl http://localhost:8080/api/academic/students \
  -H "Authorization: Bearer <accessToken>"
```

> Roles del sistema: **Direccion**, **Secretaria**, **Finanzas**, **Docente**. El contrato de cada endpoint (rol requerido, body, respuestas) está en los `openapi.json` — ver [`docs/API-DOCS.md`](API-DOCS.md).

---

## Modo A — Solo infraestructura + `dotnet run` ⭐ (recomendado)

Postgres y RabbitMQ corren en Docker; los microservicios y el Gateway se levantan con `dotnet run`. Es el flujo preferido para desarrollo: compilación incremental, hot-reload, y debugger nativo desde Rider/VS.

### 1. Levantar solo la infraestructura

```bash
docker compose -f docker-compose.yml up -d
```

> **Sin** `-f docker-compose.local.yml` y **sin** `--profile services`.  
> Levanta únicamente `cc-postgres` (localhost:**5438**) y `cc-rabbitmq` (localhost:**5672**).  
> El override `docker-compose.local.yml` elimina el port mapping de RabbitMQ — si lo incluyeras aquí, los servicios locales no podrían conectarse al broker.

Espera a que ambos contenedores estén `healthy`:

```bash
docker compose -f docker-compose.yml ps
# postgres → healthy | rabbitmq → healthy
```

### 2. Levantar cada servicio en su propia terminal

```bash
dotnet run --project src/Services/Identity/Identity.API
dotnet run --project src/Services/Academic/Academic.API
dotnet run --project src/Services/Payments/Payments.API
dotnet run --project src/Services/Attendance/Attendance.API
dotnet run --project src/Services/Notifications/Notifications.API
dotnet run --project src/Services/Analytics/Analytics.API
dotnet run --project src/Gateway/CampusConnect.Gateway
```

> Desde Rider puedes crear un *compound run configuration* que arranque los 7 a la vez.

### Puertos locales (de `launchSettings.json`)

Todas las bases viven en **una sola** instancia Postgres expuesta en `localhost:5438`; cada servicio usa su propia base dentro de ella.

| Componente | Puerto local | Base de datos (en `localhost:5438`) |
|---|---|---|
| **Gateway** | `5287` | — |
| Identity | `5245` | `identity_db` |
| Academic | `5157` | `academic_db` |
| Payments | `5235` | `payments_db` |
| Attendance | `5188` | `attendance_db` |
| Notifications | `5185` | `notifications_db` |
| Analytics | `5247` | `analytics_db` |

> En local **el Gateway escucha en `5287`** (no en 8080). El puerto 8080 solo se usa dentro de Docker.

### Cómo funciona el ruteo

El Gateway carga un archivo de Ocelot distinto según dónde corre, detectado con la variable `DOTNET_RUNNING_IN_CONTAINER`:

- **En Docker** (`DOTNET_RUNNING_IN_CONTAINER=true`) → `ocelot.json` → hostnames de la red interna (`identity-service:8080`, …).
- **En local** (variable ausente) → `ocelot.Local.json` → `localhost:<puerto launchSettings>`.

La misma variable controla el binding: en Docker se fuerza `0.0.0.0:8080`; en local se respeta el puerto de `launchSettings`. Aplica a los 6 servicios y al Gateway.

### Migraciones EF y seed inicial

Identity, Academic, Payments y Attendance aplican sus migraciones EF **automáticamente al arrancar** (`MigrateDatabase<TContext>()`), con reintentos mientras la DB inicializa. No se necesita `dotnet ef database update` manual.

Identity además ejecuta `IdentityDbInitializer.Seed` justo después: si la tabla `users` está vacía, siembra los 4 usuarios predeterminados (ver [Paso 3](#paso-3--usuarios-de-arranque-sembrados-automáticamente)). Es idempotente.

### Verificar

```bash
# Salud directa de un servicio (puerto local)
curl http://localhost:5245/health           # Identity
# Salud a través del Gateway local
curl http://localhost:5287/api/identity/health
# Barrer todos los servicios
for s in identity academic payments attendance notifications analytics; do
  printf "%-14s → " "$s"
  curl -s -o /dev/null -w "%{http_code}\n" http://localhost:5287/api/$s/health
done
```

---

## Modo B — Stack completo en Docker

Todo el sistema corre en contenedores. Útil para validar el build de producción o para demostrar el sistema sin depender de terminales abiertas.

### Levantar

```bash
docker compose -f docker-compose.yml -f docker-compose.local.yml --profile services up -d --build
```

- `-f docker-compose.local.yml` → quita el port mapping de RabbitMQ al host (los contenedores se hablan por la red interna `campusnet`, así que no lo necesitan).
- `--profile services` → incluye Gateway + los 6 microservicios.
- `--build` → reconstruye las imágenes; úsalo siempre tras cambios de código.

Espera ~20-30 s. El Gateway espera a que todos los servicios levanten sus healthchecks.

### Verificar

```bash
# Gateway en puerto 8080
curl http://localhost:8080/health
# → {"status":"ok","service":"gateway"}

# Salud de todos los servicios a través del Gateway
for s in identity academic payments attendance notifications analytics; do
  printf "%-14s → " "$s"
  curl -s -o /dev/null -w "%{http_code}\n" http://localhost:8080/api/$s/health
done
# → todos deben dar 200
```

Estado de contenedores:

```bash
docker compose -f docker-compose.yml -f docker-compose.local.yml --profile services ps
```

---

## Comandos útiles

```bash
# Logs de un servicio
docker logs -f cc-gateway
docker logs -f cc-academic-service

# Reconstruir y reiniciar un solo servicio tras cambiar su código
docker compose -f docker-compose.yml -f docker-compose.local.yml --profile services up -d --build academic-service

# Bajar todo (mantiene los volúmenes de datos)
docker compose -f docker-compose.yml -f docker-compose.local.yml --profile services down

# Bajar TODO incluyendo datos (reset limpio)
docker compose -f docker-compose.yml -f docker-compose.local.yml --profile services down -v
```

---

## Solución de problemas

### "El Gateway no se levanta"
Causa #1: olvidaste `--profile services`. El Gateway está gateado por ese perfil. Usa el comando completo del Paso 2.

### Conflictos de puertos
Si ves errores tipo `port is already allocated` para 5672/15672 (RabbitMQ), es porque ya tienes otro stack usando esos puertos. El archivo `docker-compose.local.yml` elimina el mapping al host de RabbitMQ (los contenedores siguen comunicándose por la red interna `campusnet`). Inclúyelo con `-f docker-compose.local.yml` cuando lo necesites.

> La base de datos vive ahora en **una sola** instancia Postgres (`cc-postgres`) expuesta en `localhost:5438`. El flujo `dotnet run` la necesita alcanzable en ese puerto, por eso el override NO la neutraliza. Si tuvieras un conflicto en 5438, cambia el mapping en `docker-compose.yml` (`ports: ["5438:5432"]`).

Si el puerto **8080** está ocupado, libéralo o cambia el mapping del Gateway en `docker-compose.yml` (`ports: ["8080:8080"]`).

### `Failed to bind to address http://0.0.0.0:8080: address already in use` al hacer `dotnet run`
En modo local el Gateway y los servicios **no** usan 8080 (usan su puerto de `launchSettings`). Si ves este error es porque hay un proceso viejo levantado con el código anterior (cuando el binding a 8080 no era condicional). Localiza y mata el proceso:

```bash
lsof -nP -iTCP:8080 -sTCP:LISTEN   # muestra el PID
kill <PID>
```

Luego vuelve a `dotnet run` con el código actual.

### `address already in use` en el puerto de un servicio (`dotnet run`)
Cuando cierras la terminal sin hacer `Ctrl+C`, el proceso `dotnet` queda huérfano ocupando el puerto. Para diagnosticar y limpiar:

```bash
# Verificar qué proceso ocupa un puerto (ej. Identity en 5245)
lsof -nP -iTCP:5245 -sTCP:LISTEN

# Barrer todos los puertos de los servicios de una vez
for port in 5287 5245 5157 5235 5188 5185 5247; do
  pid=$(lsof -nP -iTCP:$port -sTCP:LISTEN 2>/dev/null | awk 'NR==2{print $2}')
  [ -n "$pid" ] && echo "Puerto $port → PID $pid" && lsof -p $pid | grep -o '[A-Za-z]*.API\|CampusConnect.Gateway' | head -1
done
```

**Señal de proceso huérfano**: PID muy inferior al de los servicios activos, y el nombre de proceso es genérico (`dotnet`) en lugar del binario nombrado (`Identity.API`, `Academic.API`, etc.).

```bash
# Matar solo los procesos huérfanos (ajustar PIDs según salida del comando anterior)
kill <PID_HUERFANO>
```

> ⚠️ No mates procesos con nombre de binario propio (`Identity.API`, `CampusConnect.Gateway`) — esos son servicios correctamente levantados.

### Cambié código y no se refleja
Las imágenes se cachean. Reconstruye con `--build` (o `up -d --build <servicio>` para uno solo).

### Un servicio queda reiniciándose
Revisa sus logs: `docker logs cc-<servicio>-service`. Causas típicas: la migración EF no corrió (los servicios aplican migraciones al arrancar) o no puede conectar a su DB (espera a que la DB esté `healthy`).

### Verificar build sin levantar
```bash
docker compose -f docker-compose.yml -f docker-compose.local.yml --profile services build
```

---

## Arquitectura de despliegue (resumen)

```
                         ┌──────────────────┐
   localhost:8080  ───►  │  Gateway (Ocelot) │  /api/{servicio}/...
                         └────────┬─────────┘
        ┌──────────────┬──────────┼──────────┬──────────────┬──────────────┐
        ▼              ▼          ▼          ▼              ▼              ▼
   identity        academic    payments   attendance   notifications   analytics
      │               │           │           │            (stub)         (stub)
      └───────────────┴───────────┴───────────┴───────────────┘
                              ▼
        cc-postgres  (localhost:5438) — una instancia, una base por servicio:
        identity_db · academic_db · payments_db · attendance_db · notifications_db · analytics_db
                         │           │           │
                         └─────── RabbitMQ (event bus) ───────┘
```

- Cada servicio tiene su **propia base de datos lógica** (ningún servicio accede a la base de otro), todas alojadas en una sola instancia Postgres `cc-postgres`.
- La comunicación asíncrona entre servicios es por **RabbitMQ** (eventos de integración con patrón outbox).
- El **Gateway (Ocelot)** es el único punto de entrada; enruta `/api/{servicio}/{...}` al contenedor correspondiente y valida el JWT en las rutas de negocio (las rutas `/health` quedan abiertas).
