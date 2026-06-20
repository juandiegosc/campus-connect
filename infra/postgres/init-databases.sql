-- Crea una base de datos por microservicio dentro de la MISMA instancia Postgres.
-- Lo ejecuta el entrypoint de la imagen oficial de postgres SOLO en la primera
-- inicialización del volumen (carpeta /docker-entrypoint-initdb.d).
-- Owner = POSTGRES_USER (campus), que es superusuario.

CREATE DATABASE identity_db;
CREATE DATABASE academic_db;
CREATE DATABASE payments_db;
CREATE DATABASE attendance_db;
CREATE DATABASE notifications_db;
CREATE DATABASE analytics_db;
