# Documentación de APIs — CampusConnect 360

Cada microservicio expone su contrato HTTP completo en formato **OpenAPI 3.1** (generación nativa de .NET 10, sin Swashbuckle). Pensado para que el equipo de frontend integre cada endpoint sin leer el código del backend.

## Dónde está la documentación

Cada servicio tiene un archivo `openapi.json` **commiteado** en su carpeta `Documentation/`:

| Servicio | Archivo | Endpoint runtime |
|---|---|---|
| Identity | `src/Services/Identity/Identity.API/Documentation/openapi.json` | `GET /openapi/v1.json` |
| Academic | `src/Services/Academic/Academic.API/Documentation/openapi.json` | `GET /openapi/v1.json` |
| Payments | `src/Services/Payments/Payments.API/Documentation/openapi.json` | `GET /openapi/v1.json` |
| Attendance | `src/Services/Attendance/Attendance.API/Documentation/openapi.json` | `GET /openapi/v1.json` |
| Notifications | `src/Services/Notifications/Notifications.API/Documentation/openapi.json` | `GET /openapi/v1.json` *(stub: solo health)* |
| Analytics | `src/Services/Analytics/Analytics.API/Documentation/openapi.json` | `GET /openapi/v1.json` *(stub: solo health)* |

El archivo se **regenera automáticamente en cada `dotnet build`** (paquete `Microsoft.Extensions.ApiDescription.Server`), así que siempre refleja el código actual.

## Qué contiene cada documento

- **`info`**: título, descripción y versión del servicio.
- **`paths`**: cada endpoint con `summary`, `description` (en español), parámetros, body y respuestas.
- **`components.schemas`**: el shape tipado de cada request y response DTO (incluye enums como strings).
- **`components.securitySchemes.Bearer`**: el esquema JWT.
- **`security` por operación**: cada endpoint que requiere token lo declara explícitamente. Los públicos (login, refresh, health) **no** lo declaran → se ven sin candado.

## Cómo consumirla (frontend)

1. **Importar a Postman / Insomnia / Bruno**: `Import → File → openapi.json`. Genera la colección completa con ejemplos.
2. **UI interactiva (opcional)**: el JSON es compatible con [Scalar](https://scalar.com/) o Swagger UI — apuntarlos al archivo o al endpoint runtime `/openapi/v1.json`.
3. **Generar cliente tipado**: `openapi-generator` o `nswag` sobre el `openapi.json` produce un SDK TypeScript.

## Autenticación (flujo para probar endpoints protegidos)

```
1. POST /api/identity/auth/login   { username, password }   → { accessToken, refreshToken, ... }
2. En cada request protegido:      Authorization: Bearer {accessToken}
3. Cuando expire:                  POST /api/identity/auth/refresh   { refreshToken }  → nuevo par
```

Roles del sistema: **Direccion**, **Secretaria**, **Finanzas**, **Docente**. La `description` de cada endpoint indica el rol requerido. Todos los servicios validan el MISMO JWT (misma signing key) — un token sirve para todo el sistema.

## Acceso vía Gateway

En ejecución local todo pasa por el Gateway (Ocelot) en `http://localhost:8080`, con el prefijo del servicio:
`/api/identity/...`, `/api/academic/...`, `/api/payments/...`, `/api/attendance/...`.
