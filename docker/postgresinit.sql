-- Create Debezium user if not exists
DO
$$
BEGIN
   IF NOT EXISTS (SELECT FROM pg_catalog.pg_roles WHERE rolname = 'debezium') THEN
      CREATE USER debezium WITH PASSWORD 'debeziumpassword';
      GRANT CONNECT ON DATABASE postgres TO debezium;
      GRANT USAGE ON SCHEMA public TO debezium;
      --GRANT SELECT ON ALL TABLES IN SCHEMA public TO debezium;
      --ALTER DEFAULT PRIVILEGES IN SCHEMA public GRANT SELECT ON TABLES TO debezium;
      GRANT ALL PRIVILEGES ON SCHEMA public TO debezium;
      ALTER DEFAULT PRIVILEGES IN SCHEMA public GRANT ALL ON TABLES TO debezium;
      ALTER USER debezium WITH SUPERUSER; -- required to be able to disable binlogs from c# .net application
   END IF;
END
$$;

-- Create Data Changes user if not exists
DO
$$
BEGIN
   IF NOT EXISTS (SELECT FROM pg_catalog.pg_roles WHERE rolname = 'datachangesuser') THEN
      CREATE USER datachangesuser WITH PASSWORD 'datachangespassword';
   END IF;
END
$$;

-- Create destination database if not exists
DO
$$
BEGIN
   IF NOT EXISTS (SELECT FROM pg_database WHERE datname = 'db_chinook2') THEN
      RAISE NOTICE 'Creating database db_chinook2 failed as database already exists';
   END IF;
END
$$;

-- This part should be run separately
CREATE DATABASE db_chinook2;
GRANT ALL PRIVILEGES ON DATABASE db_chinook2 TO debezium;
