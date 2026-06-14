# CampusConnect 360 — Contratos de API y Eventos

Define los contratos REST de cada servicio y el esquema completo de los eventos de integración. Acompaña a docs/01-planificacion-backend.md.

Convenciones generales:

- Todas las rutas pasan por el Gateway con el prefijo de servicio (por ejemplo /api/academic).
- Todas las respuestas de error usan un formato uniforme ProblemDetails (RFC 7807): { type, title, status, detail, traceId }.
- Toda petición autenticada lleva Authorization: Bearer {jwt}. El traceId/CorrelationId se propaga en el header X-Correlation-Id.
- Los Command devuelven Result; en la API se traduce a 200/201 o al código de error correspondiente.

Roles: Secretaria, Finanzas, Docente, Direccion.

---

## 1. Identity Service

Base: identity_db. Responsabilidad: usuarios, roles y emisión de JWT. No publica ni consume eventos.

### Endpoints

POST /api/identity/auth/login
Acceso: anónimo.
Request:
```json
{ "username": "secretaria01", "password": "..." }
```
Response 200:
```json
{
  "accessToken": "eyJhbGci...",
  "refreshToken": "b1c2...",
  "expiresAt": "2026-06-13T12:30:00Z",
  "role": "Secretaria",
  "fullName": "Ana Pérez"
}
```
Errores: 401 credenciales inválidas.

POST /api/identity/auth/refresh
Acceso: anónimo (requiere refreshToken válido).
Request: { "refreshToken": "b1c2..." }
Response 200: mismo cuerpo que login.
Errores: 401 token expirado o revocado.

GET /api/identity/users/me
Acceso: autenticado.
Response 200: { "userId", "username", "fullName", "role" }

POST /api/identity/users
Acceso: Direccion (administración). Usado también por datos semilla.
Request: { "username", "password", "fullName", "role" }
Response 201: { "userId" }

Claims del JWT: sub (userId), name (fullName), role, schoolId, y exp/iss/aud estándar. Académico y el resto resuelven la identidad desde estos claims, nunca desde IHttpContextAccessor en los handlers.

---

## 2. Academic Service

Base: academic_db. Publica StudentEnrolled y StudentStatusUpdated. Consume PaymentConfirmed.

### Endpoints

POST /api/academic/students
Acceso: Secretaria. Registra estudiante y crea/confirma matrícula. Publica StudentEnrolled tras commit (outbox).
Request:
```json
{
  "fullName": "Luis Gómez",
  "documentId": "0102030405",
  "grade": "8vo EGB",
  "schoolId": "SCH-001",
  "guardianName": "María Gómez",
  "guardianEmail": "maria@example.com"
}
```
Response 201:
```json
{ "studentId": "STU-001", "enrollmentId": "ENR-001", "status": "Active" }
```
Errores: 400 validación (FluentValidation), 409 documento duplicado.

GET /api/academic/students
Acceso: Secretaria, Direccion. Lista paginada.
Query: ?page=1&pageSize=20&grade=8vo&search=...
Response 200: { "items": [ { "studentId", "fullName", "grade", "academicStatus", "financialStatus" } ], "total" }

GET /api/academic/students/{id}
Acceso: Secretaria, Direccion. Ficha del estudiante.
Response 200:
```json
{
  "studentId": "STU-001",
  "fullName": "Luis Gómez",
  "documentId": "0102030405",
  "grade": "8vo EGB",
  "schoolId": "SCH-001",
  "academicStatus": "Active",
  "financialStatus": "Pending",
  "guardian": { "name": "María Gómez", "email": "maria@example.com" }
}
```

GET /api/academic/students/{id}/status
Acceso: interno (servicio Pagos) y Secretaria. Versión ligera para validación síncrona.
Response 200: { "studentId", "exists": true, "academicStatus": "Active", "financialStatus": "Pending" }
Errores: 404 estudiante inexistente.

GET /api/academic/students/{id}/events
Acceso: Secretaria, Direccion. Historial básico de eventos asociados.
Response 200: { "items": [ { "eventType", "occurredAt", "correlationId" } ] }

### Consumo de eventos

PaymentConfirmed: actualiza financialStatus del estudiante a Paid; publica StudentStatusUpdated. Idempotente por Inbox (no reprocesa el mismo EventId).

---

## 3. Payments Service

Base: payments_db. Publica PaymentConfirmed. Consume StudentEnrolled (réplica mínima de estudiantes para poder listar sin tocar academic_db).

### Endpoints

GET /api/payments/students
Acceso: Finanzas. Lista estudiantes matriculados (desde la réplica local).
Response 200: { "items": [ { "studentId", "fullName", "grade", "financialStatus" } ] }

POST /api/payments/obligations
Acceso: Finanzas. Registra una obligación de pago o simula deuda.
Request: { "studentId": "STU-001", "concept": "Matrícula", "amount": 150.00, "dueDate": "2026-07-01" }
Response 201: { "obligationId": "OBL-001", "status": "Pending" }

GET /api/payments/obligations?status=Pending
Acceso: Finanzas. Lista pendientes o confirmados (status=Pending|Confirmed).
Response 200: { "items": [ { "obligationId", "studentId", "fullName", "concept", "amount", "status", "dueDate" } ] }

POST /api/payments/obligations/{id}/confirm
Acceso: Finanzas. Confirma el pago. Antes de registrar, valida el estudiante por HTTP síncrono contra Académico (GET /students/{id}/status) con Polly. Tras commit, publica PaymentConfirmed (outbox).
Request: { "method": "Transfer", "reference": "TRX-99887" }
Response 200: { "obligationId": "OBL-001", "status": "Confirmed", "paymentId": "PAY-001", "confirmedAt": "..." }
Errores: 404 estudiante inexistente, 409 obligación ya confirmada, 503 Académico no disponible (circuit breaker abierto).

### Consumo de eventos

StudentEnrolled: inserta o actualiza la réplica local de estudiante (Message Translator: evento -> modelo local). Idempotente.

---

## 4. Attendance / Wellbeing Service

Base: attendance_db. Publica AttendanceRecorded e IncidentReported. Consume StudentEnrolled.

### Endpoints

GET /api/attendance/students
Acceso: Docente. Lista estudiantes (réplica local).
Response 200: { "items": [ { "studentId", "fullName", "grade" } ] }

POST /api/attendance/records
Acceso: Docente. Registra asistencia o ausencia. Publica AttendanceRecorded.
Request: { "studentId": "STU-001", "date": "2026-06-13", "status": "Present" }  // Present | Absent | Late
Response 201: { "recordId": "ATT-001", "status": "Present" }

POST /api/attendance/incidents
Acceso: Docente. Registra incidente o novedad. Publica IncidentReported.
Request: { "studentId": "STU-001", "type": "Behavior", "severity": "Medium", "description": "..." }
Response 201: { "incidentId": "INC-001" }

GET /api/attendance/students/{id}/history
Acceso: Docente, Direccion. Historial de asistencia e incidentes del estudiante.
Response 200: { "attendance": [ ... ], "incidents": [ ... ] }

### Consumo de eventos

StudentEnrolled: actualiza la réplica local de estudiante. Idempotente.

---

## 5. Notifications Service

Base: notifications_db. Consume StudentEnrolled, PaymentConfirmed, IncidentReported, AttendanceRecorded y el comando SendNotificationCommand (point-to-point). Publica NotificationSent y NotificationFailed.

### Endpoints

GET /api/notifications
Acceso: Direccion. Lista las notificaciones simuladas generadas.
Response 200: { "items": [ { "notificationId", "studentId", "channel", "message", "status", "createdAt", "correlationId" } ] }

GET /api/notifications/students/{id}
Acceso: Direccion, Secretaria. Notificaciones de un estudiante.
Response 200: { "items": [ ... ] }

### Comportamiento

Por cada evento consumido genera una notificación simulada (no envía correo real; registra el intento). Si la generación falla de forma controlada, publica NotificationFailed para que Analítica contabilice errores. El despacho concreto puede dispararse vía SendNotificationCommand para evidenciar point-to-point.

---

## 6. Analytics Service

Base: analytics_db. Consume todos los eventos. No expone escritura. Es el modelo de lectura (CQRS a nivel de sistema) que alimenta el dashboard directivo.

### Endpoints

GET /api/analytics/dashboard
Acceso: Direccion. Indicadores consolidados.
Response 200:
```json
{
  "totalEnrolledStudents": 124,
  "confirmedPayments": 98,
  "pendingPayments": 26,
  "attendanceRecords": 540,
  "reportedIncidents": 12,
  "processedEvents": 1820,
  "failedMessages": 3,
  "ecosystemStatus": "Healthy",
  "lastUpdatedAt": "2026-06-13T11:00:00Z"
}
```

GET /api/analytics/events
Acceso: Direccion. Registro de eventos procesados (trazabilidad).
Query: ?type=PaymentConfirmed&correlationId=...
Response 200: { "items": [ { "eventId", "eventType", "occurredAt", "correlationId", "entityId" } ] }

### Comportamiento

Cada consumidor incrementa o actualiza una tabla de proyección (read model). El indicador failedMessages se nutre de NotificationFailed y de un monitor de las colas _error. ecosystemStatus se deriva de los health checks de los servicios.

---

## 7. Catálogo de eventos de integración

Todos heredan de IntegrationEvent: EventId (Guid), EventType (string), OccurredAt (UTC), CorrelationId (string).

### StudentEnrolled
Publicador: Académico. Consumidores: Notificaciones, Analítica, Pagos, Asistencia. Patrón: Pub/Sub.
```json
{
  "eventId": "evt-001",
  "eventType": "StudentEnrolled",
  "occurredAt": "2026-07-15T10:30:00Z",
  "correlationId": "corr-20260715-001",
  "studentId": "STU-001",
  "schoolId": "SCH-001",
  "grade": "8vo EGB",
  "fullName": "Luis Gómez"
}
```

### PaymentConfirmed
Publicador: Pagos. Consumidores: Académico, Notificaciones, Analítica. Patrón: Pub/Sub.
```json
{
  "eventId": "evt-002",
  "eventType": "PaymentConfirmed",
  "occurredAt": "2026-07-15T10:45:00Z",
  "correlationId": "corr-20260715-001",
  "paymentId": "PAY-001",
  "obligationId": "OBL-001",
  "studentId": "STU-001",
  "amount": 150.00,
  "method": "Transfer"
}
```

### AttendanceRecorded
Publicador: Asistencia. Consumidores: Analítica (y Notificaciones cuando aplique). Patrón: Pub/Sub.
```json
{
  "eventId": "evt-003",
  "eventType": "AttendanceRecorded",
  "occurredAt": "2026-07-15T08:00:00Z",
  "correlationId": "corr-20260715-002",
  "recordId": "ATT-001",
  "studentId": "STU-001",
  "date": "2026-07-15",
  "status": "Present"
}
```

### IncidentReported
Publicador: Asistencia. Consumidores: Notificaciones, Analítica. Patrón: Pub/Sub.
```json
{
  "eventId": "evt-004",
  "eventType": "IncidentReported",
  "occurredAt": "2026-07-15T09:10:00Z",
  "correlationId": "corr-20260715-003",
  "incidentId": "INC-001",
  "studentId": "STU-001",
  "type": "Behavior",
  "severity": "Medium"
}
```

### StudentStatusUpdated
Publicador: Académico. Consumidores: Analítica. Patrón: Pub/Sub.
```json
{
  "eventId": "evt-005",
  "eventType": "StudentStatusUpdated",
  "occurredAt": "2026-07-15T10:46:00Z",
  "correlationId": "corr-20260715-001",
  "studentId": "STU-001",
  "academicStatus": "Active",
  "financialStatus": "Paid"
}
```

### NotificationSent
Publicador: Notificaciones. Consumidores: Analítica. Patrón: Pub/Sub.
```json
{
  "eventId": "evt-006",
  "eventType": "NotificationSent",
  "occurredAt": "2026-07-15T10:31:00Z",
  "correlationId": "corr-20260715-001",
  "notificationId": "NTF-001",
  "studentId": "STU-001",
  "channel": "Email",
  "triggerEvent": "StudentEnrolled"
}
```

### NotificationFailed
Publicador: Notificaciones. Consumidores: Analítica. Patrón: Pub/Sub.
```json
{
  "eventId": "evt-007",
  "eventType": "NotificationFailed",
  "occurredAt": "2026-07-15T10:31:05Z",
  "correlationId": "corr-20260715-001",
  "notificationId": "NTF-001",
  "studentId": "STU-001",
  "reason": "SimulatedFailure",
  "triggerEvent": "StudentEnrolled"
}
```

### SendNotificationCommand
Emisor: servicios de negocio. Destinatario: Notificaciones (cola única). Patrón: Point-to-Point. Es un comando, no un evento: se envía con Send, no con Publish.
```json
{
  "messageId": "cmd-001",
  "correlationId": "corr-20260715-001",
  "studentId": "STU-001",
  "channel": "Email",
  "template": "Welcome",
  "triggerEvent": "StudentEnrolled"
}
```

## 8. Trazabilidad por CorrelationId

Un flujo completo comparte el mismo CorrelationId, lo que permite reconstruir la cadena en los logs y en GET /api/analytics/events. Ejemplo del flujo de matrícula y pago:

```
corr-20260715-001
  StudentEnrolled        (Académico publica)
  -> SendNotificationCommand (a Notificaciones)
  -> NotificationSent     (Notificaciones publica)
  PaymentConfirmed        (Pagos publica)
  -> StudentStatusUpdated (Académico publica al actualizar estado financiero)
  -> NotificationSent     (Notificaciones publica)
```
