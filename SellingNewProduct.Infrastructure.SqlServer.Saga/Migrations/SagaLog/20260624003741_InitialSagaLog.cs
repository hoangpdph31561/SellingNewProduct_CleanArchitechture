using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SellingNewProduct.Infrastructure.SqlServer.Saga.Migrations.SagaLog
{
    /// <inheritdoc />
    public partial class InitialSagaLog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SagaTransactions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    StartedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    FinishedUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Steps = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SagaTransactions", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SagaTransactions");
        }
    }
}
