using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using RaceDay.Api.Data;

#nullable disable

namespace RaceDay.Api.Migrations
{
    /// <summary>Part 3: adds the Azure Blob Storage banner reference to Events.</summary>
    [DbContext(typeof(RaceDayDbContext))]
    [Migration("20261107120000_AddEventBanner")]
    public partial class AddEventBanner : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "BannerBlobName",
                table: "Events",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BannerBlobName",
                table: "Events");
        }
    }
}
