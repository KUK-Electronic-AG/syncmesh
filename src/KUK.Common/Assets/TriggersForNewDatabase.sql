CREATE EXTENSION IF NOT EXISTS "uuid-ossp";

-- Function for AddressOutbox (conditional creation)
DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_proc WHERE proname = 'trg_addresses_insert_func') THEN
        EXECUTE $outer_execute$
        CREATE OR REPLACE FUNCTION trg_addresses_insert_func()
        RETURNS TRIGGER AS $inner_func$
        BEGIN
            IF NOT EXISTS (
                SELECT 1 
                FROM "AddressMappings"
                WHERE 
                    COALESCE(upper(trim(address_composite_key_street)), '') = COALESCE(upper(trim(NEW."Street")), '')
                    AND COALESCE(upper(trim(address_composite_key_city)), '') = COALESCE(upper(trim(NEW."City")), '')
                    AND COALESCE(upper(trim(address_composite_key_state)), '') = COALESCE(upper(trim(NEW."State")), '')
                    AND COALESCE(upper(trim(address_composite_key_country)), '') = COALESCE(upper(trim(NEW."Country")), '')
                    AND COALESCE(upper(trim(address_composite_key_postal_code)), '') = COALESCE(upper(trim(NEW."PostalCode")), '')
            ) THEN
                INSERT INTO "AddressOutbox" (event_id, aggregate_id, aggregate_type, event_type, payload, unique_identifier)
                VALUES(
                    uuid_generate_v4(),
                    NEW."AddressId",
                    $str$ADDRESS$str$,
                    $str$CREATED$str$,
                    row_to_json(NEW)::jsonb,
                    uuid_generate_v4()
                );
            END IF;
            RETURN NEW;
        END;
        $inner_func$ LANGUAGE plpgsql;
        $outer_execute$;
    END IF;
END $$;

-- Trigger for Addresses (conditional creation)
DO $$
BEGIN
    IF NOT EXISTS (SELECT 1
                   FROM   pg_trigger
                   WHERE  tgname = 'trg_addresses_insert'
                   AND    tgrelid = 'public."Addresses"'::regclass
                  ) THEN
        CREATE TRIGGER trg_addresses_insert
        AFTER INSERT ON "Addresses"
        FOR EACH ROW EXECUTE PROCEDURE trg_addresses_insert_func();
    END IF;
END $$;

-- Function for CustomerOutbox (conditional creation)
DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_proc WHERE proname = 'trg_customers_insert_func') THEN
       EXECUTE $outer_execute$
        CREATE OR REPLACE FUNCTION trg_customers_insert_func()
        RETURNS TRIGGER AS $inner_func$
        BEGIN
            IF NOT EXISTS (
                SELECT 1 
                FROM "CustomerMappings"
                WHERE "NewCustomerId" = NEW."CustomerId"
            ) THEN
                INSERT INTO "CustomerOutbox" (event_id, aggregate_id, aggregate_type, event_type, payload, unique_identifier)
                VALUES(
                    uuid_generate_v4(),
                    NEW."CustomerId",
                    $str$CUSTOMER$str$,
                    $str$CREATED$str$,
                    row_to_json(NEW)::jsonb,
                    uuid_generate_v4()
                );
            END IF;
            RETURN NEW;
        END;
        $inner_func$ LANGUAGE plpgsql;
        $outer_execute$;
    END IF;
END $$;

-- Trigger for Customers (conditional creation)
DO $$
BEGIN
    IF NOT EXISTS (SELECT 1
                   FROM   pg_trigger
                   WHERE  tgname = 'trg_customers_insert'
                   AND    tgrelid = 'public."Customers"'::regclass
                  ) THEN
        CREATE TRIGGER trg_customers_insert
        AFTER INSERT ON "Customers"
        FOR EACH ROW EXECUTE PROCEDURE trg_customers_insert_func();
    END IF;
END $$;

-- Function for InvoiceOutbox (conditional creation)
DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_proc WHERE proname = 'trg_invoices_insert_func') THEN
      EXECUTE $outer_execute$
        CREATE OR REPLACE FUNCTION trg_invoices_insert_func()
        RETURNS TRIGGER AS $inner_func$
        BEGIN
            IF NOT EXISTS (
                SELECT 1 
                FROM "InvoiceMappings"
                WHERE "NewInvoiceId" = NEW."InvoiceId"
            ) THEN
                INSERT INTO "InvoiceOutbox" (event_id, aggregate_id, aggregate_type, event_type, payload, unique_identifier)
                VALUES(
                    uuid_generate_v4(),
                    NEW."InvoiceId",
                    $str$INVOICE$str$,
                    $str$CREATED$str$,
                    row_to_json(NEW)::jsonb,
                    uuid_generate_v4()
                );
            END IF;
            RETURN NEW;
        END;
        $inner_func$ LANGUAGE plpgsql;
       $outer_execute$;
    END IF;
END $$;

-- Trigger for Invoices (conditional creation)
DO $$
BEGIN
    IF NOT EXISTS (SELECT 1
                   FROM   pg_trigger
                   WHERE  tgname = 'trg_invoices_insert'
                   AND    tgrelid = 'public."Invoices"'::regclass
                  ) THEN
        CREATE TRIGGER trg_invoices_insert
        AFTER INSERT ON "Invoices"
        FOR EACH ROW EXECUTE PROCEDURE trg_invoices_insert_func();
    END IF;
END $$;

-- Function for InvoiceLineOutbox (conditional creation)
DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_proc WHERE proname = 'trg_invoicelines_insert_func') THEN
       EXECUTE $outer_execute$
        CREATE OR REPLACE FUNCTION trg_invoicelines_insert_func()
        RETURNS TRIGGER AS $inner_func$
        BEGIN
            IF NOT EXISTS (
                SELECT 1 
                FROM "InvoiceLineMappings"
                WHERE "NewInvoiceLineId" = NEW."InvoiceLineId"
            ) THEN
                INSERT INTO "InvoiceLineOutbox" (event_id, aggregate_id, aggregate_type, event_type, payload, unique_identifier)
                VALUES(
                    uuid_generate_v4(),
                    NEW."InvoiceId",
                    $str$INVOICELINE$str$,
                    $str$CREATED$str$,
                    row_to_json(NEW)::jsonb,
                    uuid_generate_v4()
                );
            END IF;
            RETURN NEW;
        END;
        $inner_func$ LANGUAGE plpgsql;
        $outer_execute$;
    END IF;
END $$;

-- Trigger for InvoiceLines (conditional creation)
DO $$
BEGIN
    IF NOT EXISTS (SELECT 1
                   FROM   pg_trigger
                   WHERE  tgname = 'trg_invoicelines_insert'
                   AND    tgrelid = 'public."InvoiceLines"'::regclass
                  ) THEN
        CREATE TRIGGER trg_invoicelines_insert
        AFTER INSERT ON "InvoiceLines"
        FOR EACH ROW EXECUTE PROCEDURE trg_invoicelines_insert_func();
    END IF;
END $$;

-- Function for AddressOutbox during update
DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_proc WHERE proname = 'trg_addresses_update_func') THEN
        EXECUTE $outer_execute$
        CREATE OR REPLACE FUNCTION trg_addresses_update_func()
        RETURNS TRIGGER AS $inner_func$
        BEGIN
            INSERT INTO "AddressOutbox" (event_id, aggregate_id, aggregate_type, event_type, payload, unique_identifier)
            VALUES(
                uuid_generate_v4(),
                NEW."AddressId",
                'ADDRESS',
                'UPDATED',
                row_to_json(NEW)::jsonb,
                uuid_generate_v4()
            );
            RETURN NEW;
        END;
        $inner_func$ LANGUAGE plpgsql;
        $outer_execute$;
    END IF;
END $$;

-- Trigger for Addresses (UPDATE)
DO $$
BEGIN
    IF NOT EXISTS (SELECT 1
                   FROM pg_trigger
                   WHERE tgname = 'trg_addresses_update'
                     AND tgrelid = 'public."Addresses"'::regclass) THEN
        CREATE TRIGGER trg_addresses_update
        AFTER UPDATE ON "Addresses"
        FOR EACH ROW EXECUTE PROCEDURE trg_addresses_update_func();
    END IF;
END $$;

-- Function for AddressOutbox during deletion
DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_proc WHERE proname = 'trg_addresses_delete_func') THEN
        EXECUTE $outer_execute$
        CREATE OR REPLACE FUNCTION trg_addresses_delete_func()
        RETURNS TRIGGER AS $inner_func$
        BEGIN
            INSERT INTO "AddressOutbox" (event_id, aggregate_id, aggregate_type, event_type, payload, unique_identifier)
            VALUES(
                uuid_generate_v4(),
                OLD."AddressId",
                'ADDRESS',
                'DELETED',
                row_to_json(OLD)::jsonb,
                uuid_generate_v4()
            );
            RETURN OLD;
        END;
        $inner_func$ LANGUAGE plpgsql;
        $outer_execute$;
    END IF;
END $$;

-- Trigger for Addresses (DELETE)
DO $$
BEGIN
    IF NOT EXISTS (SELECT 1
                   FROM pg_trigger
                   WHERE tgname = 'trg_addresses_delete'
                     AND tgrelid = 'public."Addresses"'::regclass) THEN
        CREATE TRIGGER trg_addresses_delete
        AFTER DELETE ON "Addresses"
        FOR EACH ROW EXECUTE PROCEDURE trg_addresses_delete_func();
    END IF;
END $$;

-- Function for CustomerOutbox during update
DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_proc WHERE proname = 'trg_customers_update_func') THEN
        EXECUTE $outer_execute$
        CREATE OR REPLACE FUNCTION trg_customers_update_func()
        RETURNS TRIGGER AS $inner_func$
        BEGIN
            INSERT INTO "CustomerOutbox" (event_id, aggregate_id, aggregate_type, event_type, payload, unique_identifier)
            VALUES(
                uuid_generate_v4(),
                NEW."CustomerId",
                'CUSTOMER',
                'UPDATED',
                row_to_json(NEW)::jsonb,
                uuid_generate_v4()
            );
            RETURN NEW;
        END;
        $inner_func$ LANGUAGE plpgsql;
        $outer_execute$;
    END IF;
END $$;

-- Trigger for Customers (UPDATE)
DO $$
BEGIN
    IF NOT EXISTS (SELECT 1
                   FROM pg_trigger
                   WHERE tgname = 'trg_customers_update'
                     AND tgrelid = 'public."Customers"'::regclass) THEN
        CREATE TRIGGER trg_customers_update
        AFTER UPDATE ON "Customers"
        FOR EACH ROW EXECUTE PROCEDURE trg_customers_update_func();
    END IF;
END $$;

-- Function for CustomerOutbox during deletion
DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_proc WHERE proname = 'trg_customers_delete_func') THEN
        EXECUTE $outer_execute$
        CREATE OR REPLACE FUNCTION trg_customers_delete_func()
        RETURNS TRIGGER AS $inner_func$
        BEGIN
            INSERT INTO "CustomerOutbox" (event_id, aggregate_id, aggregate_type, event_type, payload, unique_identifier)
            VALUES(
                uuid_generate_v4(),
                OLD."CustomerId",
                'CUSTOMER',
                'DELETED',
                row_to_json(OLD)::jsonb,
                uuid_generate_v4()
            );
            RETURN OLD;
        END;
        $inner_func$ LANGUAGE plpgsql;
        $outer_execute$;
    END IF;
END $$;

-- Trigger for Customers (DELETE)
DO $$
BEGIN
    IF NOT EXISTS (SELECT 1
                   FROM pg_trigger
                   WHERE tgname = 'trg_customers_delete'
                     AND tgrelid = 'public."Customers"'::regclass) THEN
        CREATE TRIGGER trg_customers_delete
        AFTER DELETE ON "Customers"
        FOR EACH ROW EXECUTE PROCEDURE trg_customers_delete_func();
    END IF;
END $$;

-- Function for InvoiceOutbox during update
DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_proc WHERE proname = 'trg_invoices_update_func') THEN
        EXECUTE $outer_execute$
        CREATE OR REPLACE FUNCTION trg_invoices_update_func()
        RETURNS TRIGGER AS $inner_func$
        BEGIN
            INSERT INTO "InvoiceOutbox" (event_id, aggregate_id, aggregate_type, event_type, payload, unique_identifier)
            VALUES(
                uuid_generate_v4(),
                NEW."InvoiceId",
                'INVOICE',
                'UPDATED',
                row_to_json(NEW)::jsonb,
                uuid_generate_v4()
            );
            RETURN NEW;
        END;
        $inner_func$ LANGUAGE plpgsql;
        $outer_execute$;
    END IF;
END $$;

-- Trigger for Invoices (UPDATE)
DO $$
BEGIN
    IF NOT EXISTS (SELECT 1
                   FROM pg_trigger
                   WHERE tgname = 'trg_invoices_update'
                     AND tgrelid = 'public."Invoices"'::regclass) THEN
        CREATE TRIGGER trg_invoices_update
        AFTER UPDATE ON "Invoices"
        FOR EACH ROW EXECUTE PROCEDURE trg_invoices_update_func();
    END IF;
END $$;

-- Function for InvoiceOutbox during deletion
DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_proc WHERE proname = 'trg_invoices_delete_func') THEN
        EXECUTE $outer_execute$
        CREATE OR REPLACE FUNCTION trg_invoices_delete_func()
        RETURNS TRIGGER AS $inner_func$
        BEGIN
            INSERT INTO "InvoiceOutbox" (event_id, aggregate_id, aggregate_type, event_type, payload, unique_identifier)
            VALUES(
                uuid_generate_v4(),
                OLD."InvoiceId",
                'INVOICE',
                'DELETED',
                row_to_json(OLD)::jsonb,
                uuid_generate_v4()
            );
            RETURN OLD;
        END;
        $inner_func$ LANGUAGE plpgsql;
        $outer_execute$;
    END IF;
END $$;

-- Trigger for Invoices (DELETE)
DO $$
BEGIN
    IF NOT EXISTS (SELECT 1
                   FROM pg_trigger
                   WHERE tgname = 'trg_invoices_delete'
                     AND tgrelid = 'public."Invoices"'::regclass) THEN
        CREATE TRIGGER trg_invoices_delete
        AFTER DELETE ON "Invoices"
        FOR EACH ROW EXECUTE PROCEDURE trg_invoices_delete_func();
    END IF;
END $$;

-- Function for InvoiceLineOutbox during update
DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_proc WHERE proname = 'trg_invoicelines_update_func') THEN
        EXECUTE $outer_execute$
        CREATE OR REPLACE FUNCTION trg_invoicelines_update_func()
        RETURNS TRIGGER AS $inner_func$
        BEGIN
            INSERT INTO "InvoiceLineOutbox" (event_id, aggregate_id, aggregate_type, event_type, payload, unique_identifier)
            VALUES(
                uuid_generate_v4(),
                NEW."InvoiceId",
                'INVOICELINE',
                'UPDATED',
                row_to_json(NEW)::jsonb,
                uuid_generate_v4()
            );
            RETURN NEW;
        END;
        $inner_func$ LANGUAGE plpgsql;
        $outer_execute$;
    END IF;
END $$;

-- Trigger for InvoiceLines (UPDATE)
DO $$
BEGIN
    IF NOT EXISTS (SELECT 1
                   FROM pg_trigger
                   WHERE tgname = 'trg_invoicelines_update'
                     AND tgrelid = 'public."InvoiceLines"'::regclass) THEN
        CREATE TRIGGER trg_invoicelines_update
        AFTER UPDATE ON "InvoiceLines"
        FOR EACH ROW EXECUTE PROCEDURE trg_invoicelines_update_func();
    END IF;
END $$;

-- Function for InvoiceLineOutbox during deletion
DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_proc WHERE proname = 'trg_invoicelines_delete_func') THEN
        EXECUTE $outer_execute$
        CREATE OR REPLACE FUNCTION trg_invoicelines_delete_func()
        RETURNS TRIGGER AS $inner_func$
        BEGIN
            INSERT INTO "InvoiceLineOutbox" (event_id, aggregate_id, aggregate_type, event_type, payload, unique_identifier)
            VALUES(
                uuid_generate_v4(),
                OLD."InvoiceId",
                'INVOICELINE',
                'DELETED',
                row_to_json(OLD)::jsonb,
                uuid_generate_v4()
            );
            RETURN OLD;
        END;
        $inner_func$ LANGUAGE plpgsql;
        $outer_execute$;
    END IF;
END $$;

-- Trigger for InvoiceLines (DELETE)
DO $$
BEGIN
    IF NOT EXISTS (SELECT 1
                   FROM pg_trigger
                   WHERE tgname = 'trg_invoicelines_delete'
                     AND tgrelid = 'public."InvoiceLines"'::regclass) THEN
        CREATE TRIGGER trg_invoicelines_delete
        AFTER DELETE ON "InvoiceLines"
        FOR EACH ROW EXECUTE PROCEDURE trg_invoicelines_delete_func();
    END IF;
END $$;

