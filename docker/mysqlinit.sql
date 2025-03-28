-- Import Chinook database
-- SOURCE /docker-entrypoint-initdb.d/Chinook_MySql_AutoIncrementPKs.sqlfile;

-- Change timezones to current posix data as of 2024
-- SOURCE /docker-entrypoint-initdb.d/timezone_posix.sqlfile;

CREATE USER 'debezium'@'%' IDENTIFIED BY 'debeziumpassword';
GRANT ALL PRIVILEGES ON *.* TO 'debezium'@'%';
GRANT SELECT, RELOAD, SHOW DATABASES, REPLICATION SLAVE, REPLICATION CLIENT ON *.* TO 'debezium'@'%';
FLUSH PRIVILEGES;

CREATE USER 'datachanges'@'%' IDENTIFIED BY 'datachangespassword';
GRANT ALL PRIVILEGES ON db_chinook1.* TO 'datachanges'@'%';
FLUSH PRIVILEGES;

-- Create outbox tables and triggers for outbox pattern

USE `db_chinook1`;

CREATE TABLE ProcessedMessages (
    UniqueIdentifier VARCHAR(255) NOT NULL,
    PRIMARY KEY (UniqueIdentifier)
);

CREATE TABLE customer_outbox (
    event_id INT AUTO_INCREMENT PRIMARY KEY,
    aggregate_id INT,
    aggregate_type VARCHAR(255) NOT NULL,
    event_type VARCHAR(255) NOT NULL,
    payload TEXT,
    unique_identifier VARCHAR(255) NOT NULL,
    created_at DATETIME(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3)
);

CREATE TABLE invoice_outbox (
    event_id INT AUTO_INCREMENT PRIMARY KEY,
    aggregate_id INT,
    aggregate_type VARCHAR(255) NOT NULL,
    event_type VARCHAR(255) NOT NULL,
    payload TEXT,
    unique_identifier VARCHAR(255) NOT NULL,
    created_at DATETIME(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3)
);

CREATE TABLE invoiceline_outbox (
    event_id INT AUTO_INCREMENT PRIMARY KEY,
    aggregate_id INT,
    aggregate_type VARCHAR(255) NOT NULL,
    event_type VARCHAR(255) NOT NULL,
    payload TEXT,
    unique_identifier VARCHAR(255) NOT NULL,
    created_at DATETIME(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3)
);

-- Triggers for customer table

DELIMITER //
CREATE TRIGGER trg_customer_insert
AFTER INSERT
ON Customer FOR EACH ROW
BEGIN
    DECLARE unique_identifier VARCHAR(36);
    SET unique_identifier = UUID();

    INSERT INTO customer_outbox (aggregate_id, aggregate_type, event_type, payload, unique_identifier)
    VALUES(
        NEW.CustomerId,
        'CUSTOMER',
        'CREATED',
        JSON_OBJECT('CustomerId', NEW.CustomerId, 'FirstName', NEW.FirstName, 'LastName', NEW.LastName, 'Company', NEW.Company, 'Address', NEW.Address, 'City', NEW.City, 'State', NEW.State, 'Country', NEW.Country, 'PostalCode', NEW.PostalCode, 'Phone', NEW.Phone, 'Fax', NEW.Fax, 'Email', NEW.Email, 'SupportRepId', NEW.SupportRepId),
        unique_identifier
    );
END //
DELIMITER ;

DELIMITER //
CREATE TRIGGER trg_customer_update
AFTER UPDATE
ON Customer FOR EACH ROW
BEGIN
    DECLARE unique_identifier VARCHAR(36);
    SET unique_identifier = UUID();

    INSERT INTO customer_outbox (aggregate_id, aggregate_type, event_type, payload, unique_identifier)
    VALUES(
        NEW.CustomerId,
        'CUSTOMER',
        'UPDATED',
        JSON_OBJECT('CustomerId', NEW.CustomerId, 'FirstName', NEW.FirstName, 'LastName', NEW.LastName, 'Company', NEW.Company, 'Address', NEW.Address, 'City', NEW.City, 'State', NEW.State, 'Country', NEW.Country, 'PostalCode', NEW.PostalCode, 'Phone', NEW.Phone, 'Fax', NEW.Fax, 'Email', NEW.Email, 'SupportRepId', NEW.SupportRepId),
        unique_identifier
    );
END //
DELIMITER ;

DELIMITER //
CREATE TRIGGER trg_customer_delete
AFTER DELETE
ON Customer FOR EACH ROW
BEGIN
    DECLARE unique_identifier VARCHAR(36);
    SET unique_identifier = UUID();

    INSERT INTO customer_outbox (aggregate_id, aggregate_type, event_type, payload, unique_identifier)
    VALUES(
        OLD.CustomerId,
        'CUSTOMER',
        'DELETED',
        JSON_OBJECT('CustomerId', OLD.CustomerId),
        unique_identifier
    );
END //
DELIMITER ;

-- Triggers for invoice table

DELIMITER //
CREATE TRIGGER trg_invoice_insert
AFTER INSERT
ON Invoice FOR EACH ROW
BEGIN
    DECLARE unique_identifier VARCHAR(36);
    SET unique_identifier = UUID();

    INSERT INTO invoice_outbox (aggregate_id, aggregate_type, event_type, payload, unique_identifier)
    VALUES(
        NEW.InvoiceId,
        'INVOICE',
        'CREATED',
        JSON_OBJECT('InvoiceId', NEW.InvoiceId, 'CustomerId', NEW.CustomerId, 'InvoiceDate', NEW.InvoiceDate, 'BillingAddress', NEW.BillingAddress, 'BillingCity', NEW.BillingCity, 'BillingState', NEW.BillingState, 'BillingCountry', NEW.BillingCountry, 'BillingPostalCode', NEW.BillingPostalCode, 'Total', NEW.Total),
        unique_identifier
    );
END //
DELIMITER ;

DELIMITER //
CREATE TRIGGER trg_invoice_update
AFTER UPDATE
ON Invoice FOR EACH ROW
BEGIN
    DECLARE unique_identifier VARCHAR(36);
    SET unique_identifier = UUID();

    INSERT INTO invoice_outbox (aggregate_id, aggregate_type, event_type, payload, unique_identifier)
    VALUES(
        NEW.InvoiceId,
        'INVOICE',
        'UPDATED',
        JSON_OBJECT('InvoiceId', NEW.InvoiceId, 'CustomerId', NEW.CustomerId, 'InvoiceDate', NEW.InvoiceDate, 'BillingAddress', NEW.BillingAddress, 'BillingCity', NEW.BillingCity, 'BillingState', NEW.BillingState, 'BillingCountry', NEW.BillingCountry, 'BillingPostalCode', NEW.BillingPostalCode, 'Total', NEW.Total),
        unique_identifier
    );
END //
DELIMITER ;

DELIMITER //
CREATE TRIGGER trg_invoice_delete
AFTER DELETE
ON Invoice FOR EACH ROW
BEGIN
    DECLARE unique_identifier VARCHAR(36);
    SET unique_identifier = UUID();

    INSERT INTO invoice_outbox (aggregate_id, aggregate_type, event_type, payload, unique_identifier)
    VALUES(
        OLD.InvoiceId,
        'INVOICE',
        'DELETED',
        JSON_OBJECT('InvoiceId', OLD.InvoiceId),
        unique_identifier
    );
END //
DELIMITER ;

-- Triggers for invoiceline table

DELIMITER //
CREATE TRIGGER trg_invoiceline_insert
AFTER INSERT
ON InvoiceLine FOR EACH ROW
BEGIN
    DECLARE unique_identifier VARCHAR(36);
    SET unique_identifier = UUID();

    INSERT INTO invoiceline_outbox (aggregate_id, aggregate_type, event_type, payload, unique_identifier)
    VALUES(
        NEW.InvoiceId,
        'INVOICELINE',
        'CREATED',
        JSON_OBJECT('InvoiceLineId', NEW.InvoiceLineId, 'InvoiceId', NEW.InvoiceId, 'TrackId', NEW.TrackId, 'UnitPrice', NEW.UnitPrice, 'Quantity', NEW.Quantity),
        unique_identifier
    );
END //
DELIMITER ;

DELIMITER //
CREATE TRIGGER trg_invoiceline_update
AFTER UPDATE
ON InvoiceLine FOR EACH ROW
BEGIN
    DECLARE unique_identifier VARCHAR(36);
    SET unique_identifier = UUID();

    INSERT INTO invoiceline_outbox (aggregate_id, aggregate_type, event_type, payload, unique_identifier)
    VALUES(
        NEW.InvoiceId,
        'INVOICELINE',
        'UPDATED',
        JSON_OBJECT('InvoiceLineId', NEW.InvoiceLineId, 'InvoiceId', NEW.InvoiceId, 'TrackId', NEW.TrackId, 'UnitPrice', NEW.UnitPrice, 'Quantity', NEW.Quantity),
        unique_identifier
    );
END //
DELIMITER ;

DELIMITER //
CREATE TRIGGER trg_invoiceline_delete
AFTER DELETE
ON InvoiceLine FOR EACH ROW
BEGIN
    DECLARE unique_identifier VARCHAR(36);
    SET unique_identifier = UUID();

    INSERT INTO invoiceline_outbox (aggregate_id, aggregate_type, event_type, payload, unique_identifier)
    VALUES(
        OLD.InvoiceId,
        'INVOICELINE',
        'DELETED',
        JSON_OBJECT('InvoiceLineId', OLD.InvoiceLineId),
        unique_identifier
    );
END //
DELIMITER ;