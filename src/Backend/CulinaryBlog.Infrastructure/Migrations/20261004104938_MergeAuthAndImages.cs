using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CulinaryBlog.Infrastructure.Migrations;

/// <summary>
/// Đồng bộ snapshot sau khi gộp các migration Auth, Category và RecipeImage.
/// Các thay đổi schema đã nằm trong các migration trước đó.
/// </summary>
public partial class MergeAuthAndImages : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) { }

    protected override void Down(MigrationBuilder migrationBuilder) { }
}
