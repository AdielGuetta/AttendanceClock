using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AttendanceClock.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddShiftAudits : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ShiftAudits",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ShiftId = table.Column<long>(type: "bigint", nullable: false),
                    ChangedByEmployeeId = table.Column<int>(type: "int", nullable: false),
                    ChangedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Field = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false),
                    OldValue = table.Column<string>(type: "varchar(40)", unicode: false, maxLength: 40, nullable: false),
                    NewValue = table.Column<string>(type: "varchar(40)", unicode: false, maxLength: 40, nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ShiftAudits", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ShiftAudits_Employees_ChangedByEmployeeId",
                        column: x => x.ChangedByEmployeeId,
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ShiftAudits_Shifts_ShiftId",
                        column: x => x.ShiftId,
                        principalTable: "Shifts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ShiftAudits_ChangedByEmployeeId",
                table: "ShiftAudits",
                column: "ChangedByEmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_ShiftAudits_ShiftId_ChangedAt",
                table: "ShiftAudits",
                columns: new[] { "ShiftId", "ChangedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ShiftAudits");
        }
    }
}
