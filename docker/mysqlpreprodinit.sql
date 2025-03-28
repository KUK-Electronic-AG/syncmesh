-- Change timezones to current posix data as of 2024
-- SOURCE /docker-entrypoint-initdb.d/timezone_posix.sqlfile;

CREATE USER 'debezium'@'%' IDENTIFIED BY 'debeziumpassword';
GRANT ALL PRIVILEGES ON *.* TO 'debezium'@'%';
GRANT SELECT, RELOAD, SHOW DATABASES, REPLICATION SLAVE, REPLICATION CLIENT ON *.* TO 'debezium'@'%';
FLUSH PRIVILEGES;

CREATE USER 'datachanges'@'%' IDENTIFIED BY 'datachangespassword';
-- GRANT ALL PRIVILEGES ON db_chinook1.* TO 'datachanges'@'%';
FLUSH PRIVILEGES;
