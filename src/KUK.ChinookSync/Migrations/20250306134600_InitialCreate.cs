using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace KUK.ChinookSync.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "public");

            migrationBuilder.CreateTable(
                name: "Addresses",
                schema: "public",
                columns: table => new
                {
                    AddressId = table.Column<Guid>(type: "uuid", nullable: false),
                    Street = table.Column<string>(type: "text", nullable: false),
                    City = table.Column<string>(type: "text", nullable: false),
                    State = table.Column<string>(type: "text", nullable: true),
                    Country = table.Column<string>(type: "text", nullable: false),
                    PostalCode = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Addresses", x => x.AddressId);
                });

            migrationBuilder.CreateTable(
                name: "AddressOutbox",
                schema: "public",
                columns: table => new
                {
                    event_id = table.Column<Guid>(type: "uuid", nullable: false),
                    aggregate_id = table.Column<Guid>(type: "uuid", nullable: false),
                    aggregate_type = table.Column<string>(type: "text", nullable: false),
                    event_type = table.Column<string>(type: "text", nullable: false),
                    payload = table.Column<string>(type: "text", nullable: false),
                    unique_identifier = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AddressOutbox", x => x.event_id);
                });

            migrationBuilder.CreateTable(
                name: "CustomerMappings",
                schema: "public",
                columns: table => new
                {
                    OldCustomerId = table.Column<int>(type: "integer", nullable: false),
                    NewCustomerId = table.Column<Guid>(type: "uuid", nullable: false),
                    MappingTimestamp = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CustomerMappings", x => x.OldCustomerId);
                });

            migrationBuilder.CreateTable(
                name: "CustomerOutbox",
                schema: "public",
                columns: table => new
                {
                    event_id = table.Column<Guid>(type: "uuid", nullable: false),
                    aggregate_id = table.Column<Guid>(type: "uuid", nullable: false),
                    aggregate_type = table.Column<string>(type: "text", nullable: false),
                    event_type = table.Column<string>(type: "text", nullable: false),
                    payload = table.Column<string>(type: "text", nullable: false),
                    unique_identifier = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CustomerOutbox", x => x.event_id);
                });

            migrationBuilder.CreateTable(
                name: "DataMigrations",
                schema: "public",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    MigrationName = table.Column<string>(type: "text", nullable: false),
                    AppliedOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DataMigrations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "InvoiceLineMappings",
                schema: "public",
                columns: table => new
                {
                    OldInvoiceLineId = table.Column<int>(type: "integer", nullable: false),
                    NewInvoiceLineId = table.Column<Guid>(type: "uuid", nullable: false),
                    MappingTimestamp = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InvoiceLineMappings", x => x.OldInvoiceLineId);
                });

            migrationBuilder.CreateTable(
                name: "InvoiceLineOutbox",
                schema: "public",
                columns: table => new
                {
                    event_id = table.Column<Guid>(type: "uuid", nullable: false),
                    aggregate_id = table.Column<Guid>(type: "uuid", nullable: false),
                    aggregate_type = table.Column<string>(type: "text", nullable: false),
                    event_type = table.Column<string>(type: "text", nullable: false),
                    payload = table.Column<string>(type: "text", nullable: false),
                    unique_identifier = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InvoiceLineOutbox", x => x.event_id);
                });

            migrationBuilder.CreateTable(
                name: "InvoiceMappings",
                schema: "public",
                columns: table => new
                {
                    OldInvoiceId = table.Column<int>(type: "integer", nullable: false),
                    NewInvoiceId = table.Column<Guid>(type: "uuid", nullable: false),
                    MappingTimestamp = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InvoiceMappings", x => x.OldInvoiceId);
                });

            migrationBuilder.CreateTable(
                name: "InvoiceOutbox",
                schema: "public",
                columns: table => new
                {
                    event_id = table.Column<Guid>(type: "uuid", nullable: false),
                    aggregate_id = table.Column<Guid>(type: "uuid", nullable: false),
                    aggregate_type = table.Column<string>(type: "text", nullable: false),
                    event_type = table.Column<string>(type: "text", nullable: false),
                    payload = table.Column<string>(type: "text", nullable: false),
                    unique_identifier = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InvoiceOutbox", x => x.event_id);
                });

            migrationBuilder.CreateTable(
                name: "ProcessedMessages",
                schema: "public",
                columns: table => new
                {
                    UniqueIdentifier = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProcessedMessages", x => x.UniqueIdentifier);
                });

            migrationBuilder.CreateTable(
                name: "AddressMappings",
                schema: "public",
                columns: table => new
                {
                    address_mapping_id = table.Column<Guid>(type: "uuid", nullable: false),
                    address_composite_key_street = table.Column<string>(type: "text", nullable: true),
                    address_composite_key_city = table.Column<string>(type: "text", nullable: true),
                    address_composite_key_state = table.Column<string>(type: "text", nullable: true),
                    address_composite_key_country = table.Column<string>(type: "text", nullable: true),
                    address_composite_key_postal_code = table.Column<string>(type: "text", nullable: true),
                    new_address_id = table.Column<Guid>(type: "uuid", nullable: false),
                    mapping_timestamp = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AddressMappings", x => x.address_mapping_id);
                    table.ForeignKey(
                        name: "FK_AddressMappings_Addresses_new_address_id",
                        column: x => x.new_address_id,
                        principalSchema: "public",
                        principalTable: "Addresses",
                        principalColumn: "AddressId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Customers",
                schema: "public",
                columns: table => new
                {
                    CustomerId = table.Column<Guid>(type: "uuid", nullable: false),
                    FirstName = table.Column<string>(type: "text", nullable: false),
                    LastName = table.Column<string>(type: "text", nullable: false),
                    AddressId = table.Column<Guid>(type: "uuid", nullable: false),
                    Phone = table.Column<string>(type: "text", nullable: true),
                    Email = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Customers", x => x.CustomerId);
                    table.ForeignKey(
                        name: "FK_Customers_Addresses_AddressId",
                        column: x => x.AddressId,
                        principalSchema: "public",
                        principalTable: "Addresses",
                        principalColumn: "AddressId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Invoices",
                schema: "public",
                columns: table => new
                {
                    InvoiceId = table.Column<Guid>(type: "uuid", nullable: false),
                    CustomerId = table.Column<Guid>(type: "uuid", nullable: false),
                    InvoiceDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    BillingAddressId = table.Column<Guid>(type: "uuid", nullable: false),
                    Total = table.Column<decimal>(type: "numeric", nullable: false),
                    TestValue3 = table.Column<decimal>(type: "numeric(18,3)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Invoices", x => x.InvoiceId);
                    table.ForeignKey(
                        name: "FK_Invoices_Addresses_BillingAddressId",
                        column: x => x.BillingAddressId,
                        principalSchema: "public",
                        principalTable: "Addresses",
                        principalColumn: "AddressId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Invoices_Customers_CustomerId",
                        column: x => x.CustomerId,
                        principalSchema: "public",
                        principalTable: "Customers",
                        principalColumn: "CustomerId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "InvoiceLines",
                schema: "public",
                columns: table => new
                {
                    InvoiceLineId = table.Column<Guid>(type: "uuid", nullable: false),
                    InvoiceId = table.Column<Guid>(type: "uuid", nullable: false),
                    TrackId = table.Column<int>(type: "integer", nullable: false),
                    UnitPrice = table.Column<decimal>(type: "numeric", nullable: false),
                    Quantity = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InvoiceLines", x => x.InvoiceLineId);
                    table.ForeignKey(
                        name: "FK_InvoiceLines_Invoices_InvoiceId",
                        column: x => x.InvoiceId,
                        principalSchema: "public",
                        principalTable: "Invoices",
                        principalColumn: "InvoiceId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AddressMappings_new_address_id",
                schema: "public",
                table: "AddressMappings",
                column: "new_address_id");

            migrationBuilder.CreateIndex(
                name: "IX_Unique_CompositeAddressKey",
                schema: "public",
                table: "AddressMappings",
                columns: new[] { "address_composite_key_street", "address_composite_key_city", "address_composite_key_state", "address_composite_key_country", "address_composite_key_postal_code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AddressOutbox_unique_identifier",
                schema: "public",
                table: "AddressOutbox",
                column: "unique_identifier");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerOutbox_unique_identifier",
                schema: "public",
                table: "CustomerOutbox",
                column: "unique_identifier");

            migrationBuilder.CreateIndex(
                name: "IX_Customers_AddressId",
                schema: "public",
                table: "Customers",
                column: "AddressId");

            migrationBuilder.CreateIndex(
                name: "IX_InvoiceLineOutbox_unique_identifier",
                schema: "public",
                table: "InvoiceLineOutbox",
                column: "unique_identifier");

            migrationBuilder.CreateIndex(
                name: "IX_InvoiceLines_InvoiceId",
                schema: "public",
                table: "InvoiceLines",
                column: "InvoiceId");

            migrationBuilder.CreateIndex(
                name: "IX_InvoiceOutbox_unique_identifier",
                schema: "public",
                table: "InvoiceOutbox",
                column: "unique_identifier");

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_BillingAddressId",
                schema: "public",
                table: "Invoices",
                column: "BillingAddressId");

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_CustomerId",
                schema: "public",
                table: "Invoices",
                column: "CustomerId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AddressMappings",
                schema: "public");

            migrationBuilder.DropTable(
                name: "AddressOutbox",
                schema: "public");

            migrationBuilder.DropTable(
                name: "CustomerMappings",
                schema: "public");

            migrationBuilder.DropTable(
                name: "CustomerOutbox",
                schema: "public");

            migrationBuilder.DropTable(
                name: "DataMigrations",
                schema: "public");

            migrationBuilder.DropTable(
                name: "InvoiceLineMappings",
                schema: "public");

            migrationBuilder.DropTable(
                name: "InvoiceLineOutbox",
                schema: "public");

            migrationBuilder.DropTable(
                name: "InvoiceLines",
                schema: "public");

            migrationBuilder.DropTable(
                name: "InvoiceMappings",
                schema: "public");

            migrationBuilder.DropTable(
                name: "InvoiceOutbox",
                schema: "public");

            migrationBuilder.DropTable(
                name: "ProcessedMessages",
                schema: "public");

            migrationBuilder.DropTable(
                name: "Invoices",
                schema: "public");

            migrationBuilder.DropTable(
                name: "Customers",
                schema: "public");

            migrationBuilder.DropTable(
                name: "Addresses",
                schema: "public");
        }
    }
}
