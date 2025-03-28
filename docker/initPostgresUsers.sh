#!/bin/bash

# Wait for PostgreSQL to be ready
while ! pg_isready -U postgres -d $POSTGRES_DB; do
  echo "PostgreSQL is not ready yet. Waiting..."
  sleep 1
done

echo "PostgreSQL is ready. Proceeding with user creation and publication."

# Create root role with superadmin permissions and password
psql -v ON_ERROR_STOP=1 -U postgres -d postgres -c "CREATE ROLE root WITH SUPERUSER PASSWORD 'rootpassword';"

psql -v ON_ERROR_STOP=1 -U $POSTGRES_USER -d db_chinook2 -c "CREATE USER debezium WITH PASSWORD 'debeziumpassword';"
psql -v ON_ERROR_STOP=1 -U $POSTGRES_USER -d db_chinook2 -c "GRANT ALL PRIVILEGES ON DATABASE $POSTGRES_DB TO debezium;"
psql -v ON_ERROR_STOP=1 -U $POSTGRES_USER -d db_chinook2 -c "GRANT ALL PRIVILEGES ON DATABASE db_chinook2 TO debezium;"
psql -v ON_ERROR_STOP=1 -U $POSTGRES_USER -d db_chinook2 -c "ALTER ROLE debezium WITH REPLICATION;"
psql -v ON_ERROR_STOP=1 -U $POSTGRES_USER -d db_chinook2 -c "CREATE USER datachanges WITH PASSWORD 'datachangespassword';"
psql -v ON_ERROR_STOP=1 -U $POSTGRES_USER -d db_chinook2 -c "GRANT ALL PRIVILEGES ON DATABASE $POSTGRES_DB TO datachanges;"
psql -v ON_ERROR_STOP=1 -U $POSTGRES_USER -d db_chinook2 -c "GRANT ALL PRIVILEGES ON DATABASE db_chinook2 TO datachanges;"

psql -v ON_ERROR_STOP=1 -U $POSTGRES_USER -d db_chinook2 -c "GRANT CREATE ON SCHEMA public TO debezium;"

psql -v ON_ERROR_STOP=1 -U $POSTGRES_USER -d db_chinook2 -c "GRANT SELECT ON ALL TABLES IN SCHEMA public TO debezium;"

psql -v ON_ERROR_STOP=1 -U $POSTGRES_USER -d db_chinook2 -c "ALTER ROLE debezium WITH REPLICATION;"

# Create publication as postgres user
# psql -v ON_ERROR_STOP=1 -U postgres -d $POSTGRES_DB -c "CREATE PUBLICATION dbz_publication FOR ALL TABLES;"
psql -v ON_ERROR_STOP=1 -U postgres -d db_chinook2 -c "CREATE PUBLICATION dbz_publication FOR ALL TABLES;"
# psql -v ON_ERROR_STOP=1 -U postgres -d db_chinook2 -c "GRANT USAGE ON PUBLICATION dbz_publication TO debezium;"

echo "New users have been created, as well as publication."