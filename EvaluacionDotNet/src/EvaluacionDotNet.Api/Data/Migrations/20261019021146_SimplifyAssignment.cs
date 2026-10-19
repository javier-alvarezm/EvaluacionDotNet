using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EvaluacionDotNet.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class SimplifyAssignment : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                IF NOT EXISTS (SELECT 1 FROM [Currency] WHERE [Symbol] = 'USD')
                    INSERT INTO [Currency] ([Symbol], [Name]) VALUES ('USD', 'US Dollar');
                IF NOT EXISTS (SELECT 1 FROM [Currency] WHERE [Symbol] = 'EUR')
                    INSERT INTO [Currency] ([Symbol], [Name]) VALUES ('EUR', 'Euro');
                """);
            migrationBuilder.DropTable(
                name: "Users");

            migrationBuilder.AlterColumn<DateTime>(
                name: "Date",
                table: "ExchangeRates",
                type: "datetime2",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "date");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<DateTime>(
                name: "Date",
                table: "ExchangeRates",
                type: "date",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "datetime2");

            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PasswordHash = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Username = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Users_Username",
                table: "Users",
                column: "Username",
                unique: true);
        }
    }
}

