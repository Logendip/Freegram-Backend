using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Freegram.Migrations
{
    /// <inheritdoc />
    public partial class AddGroupsAndMessageReads : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CreatorId",
                table: "Chats",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "MessageReads",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    MessageId = table.Column<int>(type: "integer", nullable: false),
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    ReadAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MessageReads", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MessageReads_Messages_MessageId",
                        column: x => x.MessageId,
                        principalTable: "Messages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MessageReads_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Chats_CreatorId",
                table: "Chats",
                column: "CreatorId");

            migrationBuilder.CreateIndex(
                name: "IX_MessageReads_MessageId_UserId",
                table: "MessageReads",
                columns: new[] { "MessageId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MessageReads_UserId",
                table: "MessageReads",
                column: "UserId");

            migrationBuilder.AddForeignKey(
                name: "FK_Chats_Users_CreatorId",
                table: "Chats",
                column: "CreatorId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Chats_Users_CreatorId",
                table: "Chats");

            migrationBuilder.DropTable(
                name: "MessageReads");

            migrationBuilder.DropIndex(
                name: "IX_Chats_CreatorId",
                table: "Chats");

            migrationBuilder.DropColumn(
                name: "CreatorId",
                table: "Chats");
        }
    }
}
