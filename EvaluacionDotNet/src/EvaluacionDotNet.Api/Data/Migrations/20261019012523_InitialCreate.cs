using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EvaluacionDotNet.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Currency",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Symbol = table.Column<string>(type: "varchar(3)", unicode: false, maxLength: 3, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Currency", x => x.Id);
                    table.UniqueConstraint("AK_Currency_Symbol", x => x.Symbol);
                });

            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Username = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    PasswordHash = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ExchangeRates",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BaseCurrency = table.Column<string>(type: "varchar(3)", unicode: false, maxLength: 3, nullable: false),
                    TargetCurrency = table.Column<string>(type: "varchar(3)", unicode: false, maxLength: 3, nullable: false),
                    Rate = table.Column<decimal>(type: "decimal(28,12)", precision: 28, scale: 12, nullable: false),
                    Date = table.Column<DateTime>(type: "date", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExchangeRates", x => x.Id);
                    table.CheckConstraint("CK_Rate_Positive", "[Rate] > 0 AND [BaseCurrency] <> [TargetCurrency]");
                    table.ForeignKey(
                        name: "FK_ExchangeRates_Currency_BaseCurrency",
                        column: x => x.BaseCurrency,
                        principalTable: "Currency",
                        principalColumn: "Symbol",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ExchangeRates_Currency_TargetCurrency",
                        column: x => x.TargetCurrency,
                        principalTable: "Currency",
                        principalColumn: "Symbol",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ExchangeRates_BaseCurrency_TargetCurrency_Date",
                table: "ExchangeRates",
                columns: new[] { "BaseCurrency", "TargetCurrency", "Date" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ExchangeRates_TargetCurrency",
                table: "ExchangeRates",
                column: "TargetCurrency");

            migrationBuilder.CreateIndex(
                name: "IX_Users_Username",
                table: "Users",
                column: "Username",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ExchangeRates");

            migrationBuilder.DropTable(
                name: "Users");

            migrationBuilder.DropTable(
                name: "Currency");
        }
    }
}
