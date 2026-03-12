using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCrmAndStates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ConversationStates",
                columns: table => new
                {
                    ConversationId = table.Column<string>(type: "TEXT", nullable: false),
                    Summary = table.Column<string>(type: "TEXT", nullable: false),
                    LastMessageTimestamp = table.Column<string>(type: "TEXT", nullable: false),
                    LastActivityTimestamp = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConversationStates", x => x.ConversationId);
                });

            migrationBuilder.CreateTable(
                name: "RealStateAgencies",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    Address = table.Column<string>(type: "TEXT", nullable: false),
                    Description = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RealStateAgencies", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ConversationFacts",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    ConversationId = table.Column<string>(type: "TEXT", nullable: false),
                    FactName = table.Column<string>(type: "TEXT", nullable: false),
                    FactValue = table.Column<string>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConversationFacts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ConversationFacts_ConversationStates_ConversationId",
                        column: x => x.ConversationId,
                        principalTable: "ConversationStates",
                        principalColumn: "ConversationId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "BrokersData",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    BrokerId = table.Column<string>(type: "TEXT", nullable: false),
                    DataName = table.Column<string>(type: "TEXT", nullable: false),
                    DataKey = table.Column<string>(type: "TEXT", nullable: false),
                    DataValue = table.Column<string>(type: "TEXT", nullable: false),
                    IsPreferred = table.Column<bool>(type: "INTEGER", nullable: false),
                    Description = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<string>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BrokersData", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BrokersData_Users_BrokerId",
                        column: x => x.BrokerId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RealStateBrokers",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    RealStateAgencyId = table.Column<string>(type: "TEXT", nullable: false),
                    BrokerId = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RealStateBrokers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RealStateBrokers_RealStateAgencies_RealStateAgencyId",
                        column: x => x.RealStateAgencyId,
                        principalTable: "RealStateAgencies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_RealStateBrokers_Users_BrokerId",
                        column: x => x.BrokerId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BrokersData_BrokerId",
                table: "BrokersData",
                column: "BrokerId");

            migrationBuilder.CreateIndex(
                name: "IX_ConversationFacts_ConversationId_FactName",
                table: "ConversationFacts",
                columns: new[] { "ConversationId", "FactName" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RealStateBrokers_BrokerId",
                table: "RealStateBrokers",
                column: "BrokerId");

            migrationBuilder.CreateIndex(
                name: "IX_RealStateBrokers_RealStateAgencyId",
                table: "RealStateBrokers",
                column: "RealStateAgencyId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BrokersData");

            migrationBuilder.DropTable(
                name: "ConversationFacts");

            migrationBuilder.DropTable(
                name: "RealStateBrokers");

            migrationBuilder.DropTable(
                name: "ConversationStates");

            migrationBuilder.DropTable(
                name: "RealStateAgencies");
        }
    }
}
