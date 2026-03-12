using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Bots",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    BotNumber = table.Column<string>(type: "TEXT", nullable: false),
                    BotName = table.Column<string>(type: "TEXT", nullable: false),
                    Prompt = table.Column<string>(type: "TEXT", nullable: false),
                    Soul = table.Column<string>(type: "TEXT", nullable: false),
                    IsAgent = table.Column<bool>(type: "INTEGER", nullable: false),
                    Description = table.Column<string>(type: "TEXT", nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<string>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Bots", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ConversationStates",
                columns: table => new
                {
                    ConversationId = table.Column<string>(type: "TEXT", nullable: false),
                    BrokerId = table.Column<string>(type: "TEXT", nullable: false),
                    CustomerId = table.Column<string>(type: "TEXT", nullable: false),
                    RollingSummary = table.Column<string>(type: "TEXT", nullable: false),
                    BufferJson = table.Column<string>(type: "TEXT", nullable: false),
                    BufferChars = table.Column<int>(type: "INTEGER", nullable: false),
                    LastMessageHash = table.Column<string>(type: "TEXT", nullable: false),
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
                name: "Users",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    Email = table.Column<string>(type: "TEXT", nullable: false),
                    PasswordHash = table.Column<string>(type: "TEXT", nullable: false),
                    WhatsAppNumber = table.Column<string>(type: "TEXT", nullable: false),
                    Role = table.Column<string>(type: "TEXT", nullable: false),
                    BotId = table.Column<string>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<string>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Users_Bots_BotId",
                        column: x => x.BotId,
                        principalTable: "Bots",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ConversationFacts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ConversationId = table.Column<string>(type: "TEXT", nullable: false),
                    FactName = table.Column<string>(type: "TEXT", nullable: false),
                    Value = table.Column<string>(type: "TEXT", nullable: false),
                    Confidence = table.Column<double>(type: "REAL", nullable: false),
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
                name: "Messages",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    UserId = table.Column<string>(type: "TEXT", nullable: false),
                    BotId = table.Column<string>(type: "TEXT", nullable: false),
                    Sender = table.Column<string>(type: "TEXT", nullable: false),
                    Content = table.Column<string>(type: "TEXT", nullable: false),
                    Timestamp = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Messages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Messages_Bots_BotId",
                        column: x => x.BotId,
                        principalTable: "Bots",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Messages_Users_UserId",
                        column: x => x.UserId,
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
                name: "IX_Bots_BotNumber",
                table: "Bots",
                column: "BotNumber",
                unique: true);

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
                name: "IX_Messages_BotId",
                table: "Messages",
                column: "BotId");

            migrationBuilder.CreateIndex(
                name: "IX_Messages_Timestamp",
                table: "Messages",
                column: "Timestamp");

            migrationBuilder.CreateIndex(
                name: "IX_Messages_UserId",
                table: "Messages",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_RealStateBrokers_BrokerId",
                table: "RealStateBrokers",
                column: "BrokerId");

            migrationBuilder.CreateIndex(
                name: "IX_RealStateBrokers_RealStateAgencyId",
                table: "RealStateBrokers",
                column: "RealStateAgencyId");

            migrationBuilder.CreateIndex(
                name: "IX_Users_BotId",
                table: "Users",
                column: "BotId");

            migrationBuilder.CreateIndex(
                name: "IX_Users_Email",
                table: "Users",
                column: "Email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Users_WhatsAppNumber",
                table: "Users",
                column: "WhatsAppNumber",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BrokersData");

            migrationBuilder.DropTable(
                name: "ConversationFacts");

            migrationBuilder.DropTable(
                name: "Messages");

            migrationBuilder.DropTable(
                name: "RealStateBrokers");

            migrationBuilder.DropTable(
                name: "ConversationStates");

            migrationBuilder.DropTable(
                name: "RealStateAgencies");

            migrationBuilder.DropTable(
                name: "Users");

            migrationBuilder.DropTable(
                name: "Bots");
        }
    }
}
