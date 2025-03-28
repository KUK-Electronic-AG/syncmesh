-- DO NOT Import Chinook database for MySQL 8.0 as we create it via c# .net application
-- SOURCE /docker-entrypoint-initdb.d/Chinook_MySql_AutoIncrementPKs.sqlfile;

-- Change timezones to current posix data as of 2024
-- SOURCE /docker-entrypoint-initdb.d/timezone_posix.sqlfile;

CREATE USER 'debezium'@'%' IDENTIFIED BY 'debeziumpassword';
GRANT ALL PRIVILEGES ON *.* TO 'debezium'@'%';
GRANT SELECT, RELOAD, SHOW DATABASES, REPLICATION SLAVE, REPLICATION CLIENT ON *.* TO 'debezium'@'%';
FLUSH PRIVILEGES;

CREATE DATABASE IF NOT EXISTS db_chinook2;

CREATE USER 'datachanges'@'%' IDENTIFIED BY 'datachangespassword';
GRANT ALL PRIVILEGES ON db_chinook2.* TO 'datachanges'@'%';
FLUSH PRIVILEGES;