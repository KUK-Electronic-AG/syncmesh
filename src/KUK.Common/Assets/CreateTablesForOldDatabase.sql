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