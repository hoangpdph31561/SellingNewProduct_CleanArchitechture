using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SellingNewProduct.Infrastructure.SqlServer.Saga.Migrations
{
    /// <inheritdoc />
    public partial class AddSagaCommits : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SagaCommits",
                columns: table => new
                {
                    SagaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CommittedUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SagaCommits", x => x.SagaId);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SagaCommits");
        }
    }
}
