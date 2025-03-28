﻿// <auto-generated />
using System;
using KUK.Common.Contexts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace KUK.KafkaProcessor.Migrations
{
    [DbContext(typeof(Chinook2Context))]
    [Migration("20250306134600_InitialCreate")]
    partial class InitialCreate
    {
        /// <inheritdoc />
        protected override void BuildTargetModel(ModelBuilder modelBuilder)
        {
#pragma warning disable 612, 618
            modelBuilder
                .HasDefaultSchema("public")
                .HasAnnotation("ProductVersion", "8.0.8")
                .HasAnnotation("Relational:MaxIdentifierLength", 63);

            NpgsqlModelBuilderExtensions.UseIdentityByDefaultColumns(modelBuilder);

            modelBuilder.Entity("KUK.Common.MigrationLogic.DataMigration", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("integer");

                    NpgsqlPropertyBuilderExtensions.UseIdentityByDefaultColumn(b.Property<int>("Id"));

                    b.Property<DateTime>("AppliedOn")
                        .HasColumnType("timestamp with time zone");

                    b.Property<string>("MigrationName")
                        .IsRequired()
                        .HasColumnType("text");

                    b.HasKey("Id");

                    b.ToTable("DataMigrations", "public");
                });

            modelBuilder.Entity("KUK.Common.ModelsNewSchema.Address", b =>
                {
                    b.Property<Guid>("AddressId")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uuid");

                    b.Property<string>("City")
                        .IsRequired()
                        .HasColumnType("text");

                    b.Property<string>("Country")
                        .IsRequired()
                        .HasColumnType("text");

                    b.Property<string>("PostalCode")
                        .HasColumnType("text");

                    b.Property<string>("State")
                        .HasColumnType("text");

                    b.Property<string>("Street")
                        .IsRequired()
                        .HasColumnType("text");

                    b.HasKey("AddressId");

                    b.ToTable("Addresses", "public");
                });

            modelBuilder.Entity("KUK.Common.ModelsNewSchema.Customer", b =>
                {
                    b.Property<Guid>("CustomerId")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uuid");

                    b.Property<Guid>("AddressId")
                        .HasColumnType("uuid");

                    b.Property<string>("Email")
                        .IsRequired()
                        .HasColumnType("text");

                    b.Property<string>("FirstName")
                        .IsRequired()
                        .HasColumnType("text");

                    b.Property<string>("LastName")
                        .IsRequired()
                        .HasColumnType("text");

                    b.Property<string>("Phone")
                        .HasColumnType("text");

                    b.HasKey("CustomerId");

                    b.HasIndex("AddressId");

                    b.ToTable("Customers", "public");
                });

            modelBuilder.Entity("KUK.Common.ModelsNewSchema.Invoice", b =>
                {
                    b.Property<Guid>("InvoiceId")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uuid");

                    b.Property<Guid>("BillingAddressId")
                        .HasColumnType("uuid");

                    b.Property<Guid>("CustomerId")
                        .HasColumnType("uuid");

                    b.Property<DateTime>("InvoiceDate")
                        .HasColumnType("timestamp with time zone");

                    b.Property<decimal>("TestValue3")
                        .HasColumnType("decimal(18,3)");

                    b.Property<decimal>("Total")
                        .HasColumnType("numeric");

                    b.HasKey("InvoiceId");

                    b.HasIndex("BillingAddressId");

                    b.HasIndex("CustomerId");

                    b.ToTable("Invoices", "public");
                });

            modelBuilder.Entity("KUK.Common.ModelsNewSchema.InvoiceLine", b =>
                {
                    b.Property<Guid>("InvoiceLineId")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uuid");

                    b.Property<Guid>("InvoiceId")
                        .HasColumnType("uuid");

                    b.Property<int>("Quantity")
                        .HasColumnType("integer");

                    b.Property<int>("TrackId")
                        .HasColumnType("integer");

                    b.Property<decimal>("UnitPrice")
                        .HasColumnType("numeric");

                    b.HasKey("InvoiceLineId");

                    b.HasIndex("InvoiceId");

                    b.ToTable("InvoiceLines", "public");
                });

            modelBuilder.Entity("KUK.Common.ModelsNewSchema.Mapping.AddressMapping", b =>
                {
                    b.Property<Guid>("AddressMappingId")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uuid")
                        .HasColumnName("address_mapping_id");

                    b.Property<string>("AddressCompositeKeyCity")
                        .HasColumnType("text")
                        .HasColumnName("address_composite_key_city");

                    b.Property<string>("AddressCompositeKeyCountry")
                        .HasColumnType("text")
                        .HasColumnName("address_composite_key_country");

                    b.Property<string>("AddressCompositeKeyPostalCode")
                        .HasColumnType("text")
                        .HasColumnName("address_composite_key_postal_code");

                    b.Property<string>("AddressCompositeKeyState")
                        .HasColumnType("text")
                        .HasColumnName("address_composite_key_state");

                    b.Property<string>("AddressCompositeKeyStreet")
                        .HasColumnType("text")
                        .HasColumnName("address_composite_key_street");

                    b.Property<DateTime>("MappingTimestamp")
                        .HasColumnType("timestamp with time zone")
                        .HasColumnName("mapping_timestamp");

                    b.Property<Guid>("NewAddressId")
                        .HasColumnType("uuid")
                        .HasColumnName("new_address_id");

                    b.HasKey("AddressMappingId");

                    b.HasIndex("NewAddressId");

                    b.HasIndex("AddressCompositeKeyStreet", "AddressCompositeKeyCity", "AddressCompositeKeyState", "AddressCompositeKeyCountry", "AddressCompositeKeyPostalCode")
                        .IsUnique()
                        .HasDatabaseName("IX_Unique_CompositeAddressKey");

                    b.ToTable("AddressMappings", "public");
                });

            modelBuilder.Entity("KUK.Common.ModelsNewSchema.Mapping.CustomerMapping", b =>
                {
                    b.Property<int>("OldCustomerId")
                        .HasColumnType("integer");

                    b.Property<DateTime>("MappingTimestamp")
                        .HasColumnType("timestamp with time zone");

                    b.Property<Guid>("NewCustomerId")
                        .HasColumnType("uuid");

                    b.HasKey("OldCustomerId");

                    b.ToTable("CustomerMappings", "public");
                });

            modelBuilder.Entity("KUK.Common.ModelsNewSchema.Mapping.InvoiceLineMapping", b =>
                {
                    b.Property<int>("OldInvoiceLineId")
                        .HasColumnType("integer");

                    b.Property<DateTime>("MappingTimestamp")
                        .HasColumnType("timestamp with time zone");

                    b.Property<Guid>("NewInvoiceLineId")
                        .HasColumnType("uuid");

                    b.HasKey("OldInvoiceLineId");

                    b.ToTable("InvoiceLineMappings", "public");
                });

            modelBuilder.Entity("KUK.Common.ModelsNewSchema.Mapping.InvoiceMapping", b =>
                {
                    b.Property<int>("OldInvoiceId")
                        .HasColumnType("integer");

                    b.Property<DateTime>("MappingTimestamp")
                        .HasColumnType("timestamp with time zone");

                    b.Property<Guid>("NewInvoiceId")
                        .HasColumnType("uuid");

                    b.HasKey("OldInvoiceId");

                    b.ToTable("InvoiceMappings", "public");
                });

            modelBuilder.Entity("KUK.Common.ModelsNewSchema.Outbox.BaseOutbox", b =>
                {
                    b.Property<Guid>("EventId")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uuid")
                        .HasColumnName("event_id");

                    b.Property<Guid>("AggregateId")
                        .HasColumnType("uuid")
                        .HasColumnName("aggregate_id");

                    b.Property<string>("AggregateType")
                        .IsRequired()
                        .HasColumnType("text")
                        .HasColumnName("aggregate_type");

                    b.Property<DateTime>("CreatedAt")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("timestamp with time zone")
                        .HasColumnName("created_at")
                        .HasDefaultValueSql("now()");

                    b.Property<string>("EventType")
                        .IsRequired()
                        .HasColumnType("text")
                        .HasColumnName("event_type");

                    b.Property<string>("Payload")
                        .IsRequired()
                        .HasColumnType("text")
                        .HasColumnName("payload");

                    b.Property<string>("UniqueIdentifier")
                        .IsRequired()
                        .HasColumnType("text")
                        .HasColumnName("unique_identifier");

                    b.HasKey("EventId");

                    b.HasIndex("UniqueIdentifier");

                    b.ToTable((string)null);

                    b.UseTptMappingStrategy();
                });

            modelBuilder.Entity("KUK.Common.ModelsNewSchema.Outbox.ProcessedMessagesNew", b =>
                {
                    b.Property<string>("UniqueIdentifier")
                        .HasColumnType("text");

                    b.HasKey("UniqueIdentifier");

                    b.ToTable("ProcessedMessages", "public");
                });

            modelBuilder.Entity("KUK.Common.ModelsNewSchema.Outbox.AddressOutbox", b =>
                {
                    b.HasBaseType("KUK.Common.ModelsNewSchema.Outbox.BaseOutbox");

                    b.ToTable("AddressOutbox", "public");
                });

            modelBuilder.Entity("KUK.Common.ModelsNewSchema.Outbox.CustomerOutbox", b =>
                {
                    b.HasBaseType("KUK.Common.ModelsNewSchema.Outbox.BaseOutbox");

                    b.ToTable("CustomerOutbox", "public");
                });

            modelBuilder.Entity("KUK.Common.ModelsNewSchema.Outbox.InvoiceLineOutbox", b =>
                {
                    b.HasBaseType("KUK.Common.ModelsNewSchema.Outbox.BaseOutbox");

                    b.ToTable("InvoiceLineOutbox", "public");
                });

            modelBuilder.Entity("KUK.Common.ModelsNewSchema.Outbox.InvoiceOutbox", b =>
                {
                    b.HasBaseType("KUK.Common.ModelsNewSchema.Outbox.BaseOutbox");

                    b.ToTable("InvoiceOutbox", "public");
                });

            modelBuilder.Entity("KUK.Common.ModelsNewSchema.Customer", b =>
                {
                    b.HasOne("KUK.Common.ModelsNewSchema.Address", "Address")
                        .WithMany("Customers")
                        .HasForeignKey("AddressId")
                        .OnDelete(DeleteBehavior.Restrict)
                        .IsRequired();

                    b.Navigation("Address");
                });

            modelBuilder.Entity("KUK.Common.ModelsNewSchema.Invoice", b =>
                {
                    b.HasOne("KUK.Common.ModelsNewSchema.Address", "BillingAddress")
                        .WithMany("Invoices")
                        .HasForeignKey("BillingAddressId")
                        .OnDelete(DeleteBehavior.Restrict)
                        .IsRequired();

                    b.HasOne("KUK.Common.ModelsNewSchema.Customer", "Customer")
                        .WithMany("Invoices")
                        .HasForeignKey("CustomerId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.Navigation("BillingAddress");

                    b.Navigation("Customer");
                });

            modelBuilder.Entity("KUK.Common.ModelsNewSchema.InvoiceLine", b =>
                {
                    b.HasOne("KUK.Common.ModelsNewSchema.Invoice", "Invoice")
                        .WithMany("InvoiceLines")
                        .HasForeignKey("InvoiceId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.Navigation("Invoice");
                });

            modelBuilder.Entity("KUK.Common.ModelsNewSchema.Mapping.AddressMapping", b =>
                {
                    b.HasOne("KUK.Common.ModelsNewSchema.Address", "NewAddress")
                        .WithMany()
                        .HasForeignKey("NewAddressId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.Navigation("NewAddress");
                });

            modelBuilder.Entity("KUK.Common.ModelsNewSchema.Address", b =>
                {
                    b.Navigation("Customers");

                    b.Navigation("Invoices");
                });

            modelBuilder.Entity("KUK.Common.ModelsNewSchema.Customer", b =>
                {
                    b.Navigation("Invoices");
                });

            modelBuilder.Entity("KUK.Common.ModelsNewSchema.Invoice", b =>
                {
                    b.Navigation("InvoiceLines");
                });
#pragma warning restore 612, 618
        }
    }
}
