# Introduction

The goal of this application is explained in README.md document.

# Docker and Online modes

There are two modes, Docker and Online. Docker mode requires that you have Docker installed and running (you may also need to execute 'docker-compose up -d' to download all the images before proceeding). Online mode requires that you correctly set up connection details in appsettings.Online.Staging.json (KUK.ChinookCruds), appsettings.Online.json (KUK.EventsDebugger), appsettings.IntegrationTests.Online.json (KUK.IntegrationTests) and appsettings.Online.json (KUK.KafkaProcessor) projects.

IMPORTANT: For Online mode, you may need to change suffix that is added to connector's name to each of three appsettings files as ConnectorDetails:OldConnectorSuffix and ConnectorDetails:NewConnectorSuffix. This is to avoid issues with incorrect deletion of old connector. The application in its "Quick Delete" removes ALL the connectors, irrespective of their names. But if you had some old connector, registration of new connector may fail if you don't give it previously unseen name.

As a prerequisite, you need to set which environment you want to use. It is decided by DebeziumWorker_EnvironmentDestination environment variable which can have either "Docker" or "Online" mode. Further, you can specify if the mode is Development or Staging in DebeziumWorker_Mode_Environment where Mode indicates Docker or Online.

# Running automated tests

In order to run automated tests, you need to configure your appsetting files, environment variables etc. Then you rebuild the solution and navigate to KUK.IntegrationTests project. You double click on IntegrationTests.cs file and right click inside the desired method, e.g. TestDatabaseSynchronization. Then you choose from the context menu "Run Tests". Notice that quick actions in the tests are destructive, meaning that they intend to remove various resources and have clean starting state.

# Running manual tests

Intended workflow in manual tests is as follows:
- Press "Quick Delete" button. Notice that this is destructive action.
- Press "Quick Startup" button.
- Press "Delete Processor Logs" button.
- Press "Start Processor" button. Wait for all initializations to happen. In particular, this log "Applying MigrateInvoicesAndRelatedTables" may indicate long running process (e.g. 90 seconds in our Online mode tests). Then wait for "Received snapshot event. snapshot=last." log that would indicate that receiving initial snaphost has finished.
- Press button from "Old Database", "Old Database - Complex" and "New Database" in order in which they appear in the GUI. Specifically, notice that creating some entries depends on adding new column to Customer table; otherwise these creations would fail. If you are comfortable with this basic execution order, you may experiment with your own order of execution.

You can also perform various actions by yourself in the GUI, rather than relying on "Quick Delete" and "Quick Startup". For example, you may want to do first deletion of old connectors via "Delete" button in "Connector Management", then "Delete Schema, Tables and Data" for both databases, then "Initialize Schema, Tables and Data" for old database (that would create Chinook database) and finally "Start Processor". It is up to you to create meaningful manual tests.

Notice that C# processor itself will register both connectors and create new database based on old one, using initial migrations.

# Projects in .NET solution

There are several projects in the solution:

- KUK.ChinookCruds - this application lets you interact in both Docker and Online mode with the resources using graphical user interface (it's simple web application)
- KUK.Common - library used by several projects
- KUK.EventsDebugger - separate utility that helps you to gather all the events that happened in the databases in outbox tablese in the order in which they arrive
- KUK.IntegrationTests - tests checking full flow of the application, performing various database operations like adding customers, invoices etc.
- KUK.KafkaProcessor - this C# console application (with additional endpoints) is the heart of this solution. It reads events sent from Debezium to Kafka and saves them to old or new database
- KUK.ManagementServices - auxiliary services that help managing various resources
- KUK.UnitTests - simple automated tests that check the source code

# Chinook database

The whole PoC solution is based on open-source database called Chinook. It contains invoices, invoice lines and customers, among other tables which we don't use in PoC. New database (that we want to have as destination Postgres) has the same tables, with exception for addresses. In other words, new database has addresses in separate table, whereas old database has them inside customers and invoices. Moreover, old database uses integers for primary keys, whereas new database has Guids.

# Kafka Processor

This is the most important application in the whole solution that reads database events from Kafka and saves them to the databases.

The whole processing happens in EventProcessing/DatabaseEventProcessor.cs which should be your starting point for the analysis of the code.

## Priority lists

PriorityLists is important place to make changes for your own implementation (for your own database). It specified what depends on what. For example:

```
_priorityLists = new List<List<PriorityDependency>>
{
    // Invoice -> InvoiceLine: InvoiceLine depends od Invoice, ID field is "InvoiceId"
    new List<PriorityDependency> { 
        new PriorityDependency("Invoice", "InvoiceId"), 
        new PriorityDependency("InvoiceLine", "InvoiceId") 
    },
    // Customer -> Invoice: Invoice depends on Customer, ID field is "CustomerId"
    new List<PriorityDependency> { 
        new PriorityDependency("Customer", "CustomerId"), 
        new PriorityDependency("Invoice", "CustomerId") 
    },
    // Address -> Customer: Customer depends on Address, ID field id "AddressId"
    new List<PriorityDependency> {
        new PriorityDependency("Address", "AddressId"),
        new PriorityDependency("Customer", "AddressId")
    }
};
```

## Main processing

RunEventProcessingAsync is the entry point in DatabaseEventProcessor class. It does the following:

- Validates the settings
- Creates new Kafka topics
- Registers connector that reads from old database ("old connector")
- Waits for old connector to become operational
- Initializes new database based on old database (initial migrations)
- Creates triggers in new database
- Registers connector that reads from new database ("new connector")
- Creates new Kafka topics
- Watigs fro both connectors to be opearational
- Subscribes to OldToNewTopic (where events from old connector arrive) and NewToOldTopic (events from new database)
- Creates further Kafka topics
- Starts processing by running StartConsumersAsync and ProcessDebeziumEventsAsync

## Main loops

The goal of StartConsumersAsync is to reads events from OldToNewTopic, NewToOldTopic and BufferTopic. ProcessDebeziumEventsAsync method starts reading events from EventQueueTopic.

The process of these events is such that event arrives from Debezium reading old database to OldToNewTopic and from Debezium reading new database to NewToOldTopic. It is later saved to BufferTopic. FlushTask (using StartFlushCycleAsync) reads events from BufferTopic and processes them in batches by sending to EventQueueTopic in correct order.

## Ensuring that dependencies are met

The problem with this approach is that events may arrive in incorrect order from the database. For example, invoice may arrive before inserting related customer or invoice line before related invoice. For this reason, StartFlushCycleAsync method reads events in loop and collects them in short buffer. The time for this collection is decided by BufferEventsCollectionInMilliseconds appsettings parameter (for example 500 milliseconds).

If incomplete events are captured in one cycle, e.g. we have only invoice line and we don't have related invoice, EnsureDependenciesAsync method reads new events from the buffer and checks if new event is the one we need to process related event from earlier buffer cycle (e.g. we wait for invoice if we have only invoice line because invoice line cannot be added without related invoice).

EnsureDependenciesAsync (from EventsSortingService) checks event type and priority list to see if new events should be added from next buffer events. It returns all events that are required to correctly process the given batch. Later they are sorted by SortEvents method so that we have correct order of adding various events to the final database (old or new). And finally, it sends them to EventQueueTopic in ProcessAndProduceEventAsync method and commits Kafka events (marks them as processed by KafkaProcessor application) by calling Commit on consumerBuffer.

## Saving to target databases

Further processing is done in the infinite loop in ProcessDebeziumEventsAsync method. It reads aggregate id, aggregate type and event type and saves it to the destination database (old for events originating from new database and vice versa). SynchronizeWithMySQL 

The process in ProcessDebeziumEventsAsync is as follows:

- It consumes the event from EventQueueTopic
- It extracts aggregate id, aggregate type and event type
- It skips events originating from initial snapshot
- It skips read operations
- It extracts the source and creates new syncId (Guid)
- It saves to event-sourcing database called Marten database
- It synchronizes data with destination database

This initial snaphost that is skipped originates from Debezium connectors. Basically, old-to-new connector first reads from old database the schema (this is initial snapshot) and then reads the subsequent database events (like inserts, updates and deletes).

Marten database for event sourcing is optional in this scenario. It helps to save all the events to separate database for audit and debugging purposes. This logic can be easily removed from the code by commenting out relevat piece of code (that starts LightweightSession and uses Store and SaveChangesAsync on that session).

Destination database is either new database for events originating from old database or old database for events originating from new database.

# Integration test flow

There are two main integration tests: TestDatabaseSynchronization and LoadTest.

The flow of both tests starts with:

- QuickDelete
- QuickStartup
- DeleteProcessorLogs
- StartProcessorAsync
- WaitForProcessorToFinishInitializatonAsync

QuickDelete - for Docker mode it removes all the containers needed in this scenario. For Online mode, it removes all the connectors that start with specified strings ("old-to-new-connector" and "new-to-old-connector"), deletes the schema for both old and new databases and deletes Kafka topics. Notice that these are destructive actions.

QuickStartup - for Docker mode, it starts required containers and initializes schema, tables and data for old database, for which Chinook example is used.

Then there is different flow for both the integration tests.

TestDatabaseSynchronization does the following:

- Creating customer in old database
- Creating invoice in old database
- Creating customer in new database
- Creating invoice in new database
- Adding new customer using subquery to old database
- Adding new invoice using nested query to old database
- Waiting for events to be processed
- Updating customer city in old database
- Waiting for events to be processed
- Updating customer email and address in new database
- Waiting for events to be processed
- Deleting customer in old database
- Waiting for events to be processed
- Deleting customer in new database
- Waiting for events to be processed
- Updating invoice lines in old and new database
- Waiting for events to be processed

Load test does inserts of customers and invoices in both old and new databases in a loop that lasts for several seconds. This time can be controlled by:

```
var endTime = startTime.AddSeconds(5);
```

You are encouraged to increase this time during your tests.

# Details of initialization process

InitializeSchemaTablesAndDataForOldDatabase method from SchemaInitializerService is responsible for creating schema, tables and data for old database, which is our source database. It works differently for Docker and Online mode.

For both modes, it starts with:
- Creating schema if it does not exist
- Importing database from SQL file
- Adding test value column
- Waiting for databsae connection schema and data

For Docker mode, it does as follows:

- It executes init.sql
- It copies mysql.cnf file to the container
- It changes permissions of cnf file to 644
- It restarts mysql80 (old database) container
- It waits for database connection and schema to be available

The goal of init.sql (mysqlinit.sql) file is very important. It creates debezium and datachanges users and required outbox tables, together with triggers for old database (needed for outbox pattern).

The other file, i.e. mysql.cnf, enables binlog which is crucial for Debezium to be able to work and read changes from outbox tables in old database.

For Online mode, InitializeSchemaTablesAndDataForOldDatabase method simply executes CreateTablesForOldDatabase SQL scripts because it is your responsibility to set up everything in the cloud (enable binlogs, create users etc.). It also creates the triggers.

It is important to note that both triggers creation and connectors creation is done differently (in separate methods) for Online and Docker mode. In other words, if you develop some part of the solution for Docker, you have to ensure that you make analogical changes for Online mode.

# Outbox pattern (tables and triggers)

The goal was to make as little changes to the old database as possible. However, some things could not be avoided. First of all, old database needs to be relateively new. If you use MySQL for old database, you need to upgrade it to at least MySQL 8.0 version. Moreover, some additional tables and triggers have to be created in the old database.

The application relies on outbox patterns. For example, old database contains Customer, Invoice and InvocieLine tables. Additionally, this PoC requires that customer_outbox, invoice_outbox and invoiceline_outbox tables are created. In new database we have even more tables, i.e. not only main ones (Addresses, Customers, InvoiceLines and Invoices) but also related Mappings and Outbox tables, e.g. AddressMappings and AddressOutbox tables. Moreover, both databases contain ProcessedMessages tables.

The triggers are responsible for writing data to outbox tables whenever new entry is added to the main table. Let us take an exemplary trigger that is located in KUK.Common.Assets.TriggersForNewDatabase.sql like trg_invoices_insert. It uses trg_invoices_insert_func functoin and if mapping does not exist, it adds to InvoiceOutbox table relevant values.

These values are:

- event_id
- aggregate_id
- aggregate_type
- event_type
- payload
- unique_identifier

Crucially, aggregate ids must be correctly set. For example, both invoice and invoice line have InvoiceId as aggregate id because invoice line depends on invoice.

# Connectors configurations

Connectors configuration files are present in KUK.Common.Assets as:

debezium-connector-config-1.json
debezium-connector-config-2.json
DebeziumConnectorConfigOnline1.json
DebeziumConnectorConfigOnline2.json

First two files are used for Docker mode, second two files are used for Online mode.

Online mode relies on filling in parts of the connectors configuration based on appsettings.

First connector (the one with 1 as number) is applied to read events from old database, whereas second connector (the one with 2 as number) is used to read events from new database.

# Your own implementation

If you want to modify this application to work for your own database, you need to:

- Review comments left with REMARK keyword in the code
- Set up your own initialization scripts, especially for creating outbox tables and triggers
- Correctly prepare priority lists to show dependencies between various tables and IDs
- Create initial migration for your database
- Create your own KafkaProcessor services for processing ad hoc events for your own database
- Extend EventsSortingService in CheckDependencyExistsAsync method
- Adjust unit and integration tests for your own database structure
- Test it carefully with Docker
- Implement changes in ExternalConnectionService that will work with your own cloud provider
- Test it carefully with Online, preferably doing all unit, integration and manual tests
- Implement security measures like email notifications for health check failures
- Deploy to production only if you are 100% sure that everything works correctly
- Use it at your own risk

# Further development

You are encouraged to improve or extend this application and provide your own suggestions or comments to the developer.

# Conclusion

This PoC was able to prove that achieving bidirectional synchronization between two databases is possible but far from trivial. It is worth to note that this solution may work only for small to medium size databases and not for big databases because KafkaProcessor works sequentially, i.e. it is not possible to scale it horizontally by launching new instances. The reason for this is that we wanted to ensure the correct order of events.