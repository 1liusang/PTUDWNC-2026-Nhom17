using CulinaryBlog.Domain.Constants;
using Microsoft.AspNetCore.Identity;

namespace CulinaryBlog.API.Seed;

public static class RoleSeeder
{
    private static readonly string[] DefaultRoles = [Roles.Author, Roles.Admin];

    public static async Task SeedRolesAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();

        foreach (var role in DefaultRoles)
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                await roleManager.CreateAsync(new IdentityRole(role));
            }
        }
    }
}
