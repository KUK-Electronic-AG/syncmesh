#!/bin/bash
# Download SQL file with Chinook database
curl -o /docker-entrypoint-initdb.d/Chinook_MySql_AutoIncrementPKs.sql https://github.com/lerocha/chinook-database/releases/download/v1.4.5/Chinook_MySql_AutoIncrementPKs.sql

# Import Chinook database
mysql -u root -p${DebeziumWorker_OldMySqlRootPassword} < /docker-entrypoint-initdb.d/Chinook_MySql_AutoIncrementPKs.sql

# Change database name
mysql -u root -p${DebeziumWorker_OldMySqlRootPassword} -e "RENAME DATABASE chinook_auto_increment TO db_chinook1;"
