using CulinaryBlog.Domain.Constants;
using Microsoft.AspNetCore.Authorization;

namespace CulinaryBlog.API.Authorization;

/// <summary>
/// Tên policy role-based dùng chung cho toàn bộ API (NFR-SEC-006).
/// Dùng [Authorize(Policy = AuthorizationPolicies.Author)] thay vì hardcode role string.
/// Các policy về resource-ownership (VD: RecipeAuthorizationHandler cho Module 2 -
/// Recipe) do module tương ứng tự định nghĩa và đăng ký thêm tại đây khi cần.
/// </summary>
public static class AuthorizationPolicies
{
    public const string Author = "AuthorPolicy";
    public const string Admin = "AdminPolicy";

    public static void AddCulinaryBlogPolicies(this AuthorizationOptions options)
    {
        options.AddPolicy(Author, policy => policy.RequireRole(Roles.Author, Roles.Admin));
        options.AddPolicy(Admin, policy => policy.RequireRole(Roles.Admin));
    }
}
