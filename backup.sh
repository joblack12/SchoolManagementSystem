#!/usr/bin/env bash
set -euo pipefail

if [ -f .env ]; then
    set -a
    source .env
    set +a
fi

BACKUP_DIR="./backups"
TIMESTAMP=$(date +"%Y%m%d_%H%M%S")
DB_CONTAINER="${DB_CONTAINER:-schoolmanagement-db}"
WEB_CONTAINER="${WEB_CONTAINER:-schoolmanagement-web}"
RETENTION_DAYS=14

mkdir -p "$BACKUP_DIR"

echo "[$TIMESTAMP] Starting SchoolManagementSystem backup..."

# 1. Back up SQL Server database inside container
docker exec "$DB_CONTAINER" /opt/mssql-tools18/bin/sqlcmd \
    -S localhost -U sa -P "$DB_PASSWORD" -No -Q \
    "BACKUP DATABASE [SchoolDbContext] TO DISK = '/var/opt/mssql/data/SchoolDbContext_${TIMESTAMP}.bak' WITH FORMAT, INIT, NAME = 'Full Backup';"

# Copy .bak to host and remove temporary file from container
docker cp "${DB_CONTAINER}:/var/opt/mssql/data/SchoolDbContext_${TIMESTAMP}.bak" "${BACKUP_DIR}/SchoolDbContext_${TIMESTAMP}.bak"
docker exec "$DB_CONTAINER" rm -f "/var/opt/mssql/data/SchoolDbContext_${TIMESTAMP}.bak"

# 2. Archive uploaded student/teacher/employee files
docker exec "$WEB_CONTAINER" tar -czf "/tmp/uploads_${TIMESTAMP}.tar.gz" -C /app/wwwroot uploads
docker cp "${WEB_CONTAINER}:/tmp/uploads_${TIMESTAMP}.tar.gz" "${BACKUP_DIR}/uploads_${TIMESTAMP}.tar.gz"
docker exec "$WEB_CONTAINER" rm -f "/tmp/uploads_${TIMESTAMP}.tar.gz"

# 3. Prune backups older than RETENTION_DAYS
find "$BACKUP_DIR" -type f \( -name "*.bak" -o -name "*.tar.gz" \) -mtime +${RETENTION_DAYS} -delete

echo "[$TIMESTAMP] Backup completed: ${BACKUP_DIR}/SchoolDbContext_${TIMESTAMP}.bak and uploads_${TIMESTAMP}.tar.gz"
