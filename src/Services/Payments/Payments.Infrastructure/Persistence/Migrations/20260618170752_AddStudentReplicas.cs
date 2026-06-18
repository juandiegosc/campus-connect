using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Payments.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddStudentReplicas : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ADR-055: ONLY student_replicas DDL. InboxState already exists from InitialPayments.
            // IX_InboxState_Delivered was hand-trimmed — that index was re-emitted by EF snapshot diff
            // but must NOT be applied again (table and existing indexes already in DB).
            migrationBuilder.CreateTable(
                name: "student_replicas",
                columns: table => new
                {
                    student_id = table.Column<string>(type: "character varying(26)", maxLength: 26, nullable: false),
                    full_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    grade = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    school_id = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    last_updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_student_replicas", x => x.student_id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_student_replicas_grade",
                table: "student_replicas",
                column: "grade");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // ADR-055: Down drops only student_replicas. InboxState index was trimmed from Up() — skip here.
            migrationBuilder.DropTable(
                name: "student_replicas");
        }
    }
}
