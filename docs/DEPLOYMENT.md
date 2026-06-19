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

## Paso 2 — Levantar todo el stack

```bash
docker compose -f docker-compose.yml -f docker-compose.local.yml --profile services up -d --build
```

- `--profile services` → incluye Gateway + los 6 microservicios (sin esto, solo infra).
- `-f docker-compose.local.yml` → quita los port mappings de RabbitMQ e identity-db hacia el host (evita conflictos si ya tienes otros Postgres/RabbitMQ corriendo). Ver [Conflictos de puertos](#conflictos-de-puertos).
- `--build` → reconstruye las imágenes (úsalo siempre tras cambios de código).

Espera ~15-20 s a que arranquen las DB (tienen healthcheck) y los servicios.

---

## Paso 3 — Verificar que todo responde

```bash
# Salud del Gateway (su propio endpoint, antes de Ocelot)
curl http://localhost:8080/health
# → {"status":"ok","service":"gateway"}

# Salud de cada servicio ENRUTADO por el Gateway
for s in identity academic payments attendance notifications analytics; do
  printf "%s -> " "$s"; curl -s -o /dev/null -w "%{http_code}\n" http://localhost:8080/api/$s/health
done
# → todos deben dar 200
```

Estado de los contenedores:

```bash
docker compose -f docker-compose.yml -f docker-compose.local.yml --profile services ps
```

---

## Paso 4 — Bootstrap del primer usuario (para endpoints protegidos)

`POST /api/identity/users` requiere un JWT con rol **Direccion**, pero al inicio no existe ningún usuario (problema del huevo y la gallina). Hay que sembrar el primer usuario Direccion directamente en la base.

1. Genera un hash BCrypt de tu contraseña (cualquier herramienta BCrypt, cost 12).
2. Insértalo en `identity-db`:

```bash
docker exec -i cc-identity-db psql -U campus -d identity_db <<'SQL'
INSERT INTO users (id, username, full_name, password_hash, role, is_active, created_at)
VALUES (gen_random_uuid(), 'director1', 'Director Principal', '<BCRYPT_HASH>', 'Direccion', true, NOW())
ON CONFLICT (username) DO NOTHING;
SQL
```

3. Haz login y usa el `accessToken` en las siguientes llamadas:

```bash
curl -X POST http://localhost:8080/api/identity/auth/login \
  -H 'Content-Type: application/json' \
  -d '{"username":"director1","password":"<TU_PASSWORD>"}'
# → { accessToken, refreshToken, ... }

# Llamada protegida:
curl http://localhost:8080/api/academic/students \
  -H "Authorization: Bearer <accessToken>"
```

> Roles del sistema: **Direccion**, **Secretaria**, **Finanzas**, **Docente**. El contrato de cada endpoint (rol requerido, body, respuestas) está en los `openapi.json` — ver [`docs/API-DOCS.md`](API-DOCS.md).

---

## Dos modos de ejecución

| Objetivo | Comando |
|---|---|
| **Todo el sistema** (Gateway + servicios + infra) | `docker compose -f docker-compose.yml -f docker-compose.local.yml --profile services up -d --build` |
| **Solo infraestructura** (DB + RabbitMQ) — para correr servicios desde el IDE | `docker compose -f docker-compose.yml -f docker-compose.local.yml up -d` |

El modo "solo infra" es útil cuando depuras un servicio desde Visual Studio / Rider apuntando a `localhost`.

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
Si ves errores tipo `port is already allocated` para 5672/15672 (RabbitMQ) o 5433 (Postgres), es porque ya tienes otro stack usando esos puertos. La solución ya está incluida: el archivo `docker-compose.local.yml` elimina esos mappings al host (los contenedores siguen comunicándose por la red interna `campusnet`). Asegúrate de incluir `-f docker-compose.local.yml` en TODOS los comandos.

Si el puerto **8080** está ocupado, libéralo o cambia el mapping del Gateway en `docker-compose.yml` (`ports: ["8080:8080"]`).

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
   identity-db     academic-db  payments-db attendance-db  notif-db      analytics-db
                         │           │           │
                         └─────── RabbitMQ (event bus) ───────┘
```

- Cada servicio tiene su **propia base de datos** (ningún servicio accede a la DB de otro).
- La comunicación asíncrona entre servicios es por **RabbitMQ** (eventos de integración con patrón outbox).
- El **Gateway (Ocelot)** es el único punto de entrada; enruta `/api/{servicio}/{...}` al contenedor correspondiente y valida el JWT en las rutas de negocio (las rutas `/health` quedan abiertas).
