using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddNudgeConfig : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "NudgeBrokerAfterMessages",
                table: "RealStateBrokers",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "NudgeTimeoutMinutes",
                table: "RealStateBrokers",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "NudgeBrokerAfterMessages",
                table: "RealStateAgencies",
                type: "INTEGER",
                nullable: false,
                defaultValue: 3);

            migrationBuilder.AddColumn<int>(
                name: "NudgeTimeoutMinutes",
                table: "RealStateAgencies",
                type: "INTEGER",
                nullable: false,
                defaultValue: 10);

            migrationBuilder.AddColumn<string>(
                name: "LastMessageActor",
                table: "ConversationStates",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "SignalsJson",
                table: "ConversationStates",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "ConversationTasks",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    ConversationId = table.Column<string>(type: "TEXT", nullable: false),
                    Type = table.Column<string>(type: "TEXT", nullable: false),
                    Status = table.Column<string>(type: "TEXT", nullable: false),
                    Owner = table.Column<string>(type: "TEXT", nullable: false),
                    Description = table.Column<string>(type: "TEXT", nullable: false),
                    MetadataJson = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<string>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConversationTasks", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ConversationTasks_ConversationId_Type",
                table: "ConversationTasks",
                columns: new[] { "ConversationId", "Type" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ConversationTasks");

            migrationBuilder.DropColumn(
                name: "NudgeBrokerAfterMessages",
                table: "RealStateBrokers");

            migrationBuilder.DropColumn(
                name: "NudgeTimeoutMinutes",
                table: "RealStateBrokers");

            migrationBuilder.DropColumn(
                name: "NudgeBrokerAfterMessages",
                table: "RealStateAgencies");

            migrationBuilder.DropColumn(
                name: "NudgeTimeoutMinutes",
                table: "RealStateAgencies");

            migrationBuilder.DropColumn(
                name: "LastMessageActor",
                table: "ConversationStates");

            migrationBuilder.DropColumn(
                name: "SignalsJson",
                table: "ConversationStates");
        }
    }
}
