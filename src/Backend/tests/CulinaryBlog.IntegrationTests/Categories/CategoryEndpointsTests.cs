using System.Net;
using System.Net.Http.Json;
using CulinaryBlog.IntegrationTests.Auth;
using CulinaryBlog.IntegrationTests.Infrastructure;
using CulinaryBlog.Application.Features.Auth.Abstractions;
using CulinaryBlog.Domain.Auth;
using Microsoft.Extensions.DependencyInjection;

namespace CulinaryBlog.IntegrationTests.Categories;

[Collection(PostgresCollection.Name)]
public sealed class CategoryEndpointsTests(PostgresApiFactory factory)
{
    [Fact]
    public async Task Admin_CanCreateUpdateAndDeleteEmptyCategory()
    {
        using var admin = CreateClient(Roles.Admin);
        var created = await admin.PostAsJsonAsync("/api/v1/categories", new
        {
            name = $"Danh mục thử {Guid.NewGuid():N}",
            orderIndex = 50,
        });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var category = await created.Content.ReadFromJsonAsync<CategoryResponse>();
        Assert.NotNull(category);

        var updated = await admin.PutAsJsonAsync($"/api/v1/categories/{category.Id}", new
        {
            name = $"Danh mục đã sửa {Guid.NewGuid():N}",
            orderIndex = 51,
        });
        Assert.Equal(HttpStatusCode.OK, updated.StatusCode);
        var updatedCategory = await updated.Content.ReadFromJsonAsync<CategoryResponse>();
        Assert.NotNull(updatedCategory);
        Assert.Equal(category.Id, updatedCategory.Id);
        Assert.Equal(51, updatedCategory.OrderIndex);

        var deleted = await admin.DeleteAsync($"/api/v1/categories/{category.Id}");
        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
    }

    [Fact]
    public async Task CreateCategory_WithExistingName_Returns409WithCategoryCode()
    {
        using var admin = CreateClient(Roles.Admin);
        var name = $"Danh mục thử {Guid.NewGuid():N}";
        var payload = new { name, description = "Dùng kiểm thử API", orderIndex = 50 };

        var first = await admin.PostAsJsonAsync("/api/v1/categories", payload);
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);

        var duplicate = await admin.PostAsJsonAsync("/api/v1/categories", new { name = name.ToUpperInvariant(), payload.description, payload.orderIndex });

        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
        Assert.Equal("CATEGORY_NAME_EXISTS", await AuthApi.ReadErrorCodeAsync(duplicate));
    }

    [Fact]
    public async Task DeleteCategory_WithDraftRecipe_Returns409AndRecipeCount()
    {
        using var admin = CreateClient(Roles.Admin);
        using var author = CreateClient(Roles.Author);
        var created = await admin.PostAsJsonAsync("/api/v1/categories", new
        {
            name = $"Danh mục thử {Guid.NewGuid():N}",
            orderIndex = 50,
        });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var category = await created.Content.ReadFromJsonAsync<CategoryResponse>();
        Assert.NotNull(category);

        var recipe = await author.PostAsJsonAsync("/api/v1/recipes", new
        {
            title = $"Công thức thử {Guid.NewGuid():N}",
            description = "Công thức để kiểm tra xóa danh mục",
            prepTimeMinutes = 10,
            cookTimeMinutes = 10,
            servings = 2,
            difficulty = 0,
            categoryId = category.Id,
        });
        Assert.Equal(HttpStatusCode.Created, recipe.StatusCode);

        var deleted = await admin.DeleteAsync($"/api/v1/categories/{category.Id}");

        Assert.Equal(HttpStatusCode.Conflict, deleted.StatusCode);
        Assert.Equal("CATEGORY_DELETE_HAS_RECIPES", await AuthApi.ReadErrorCodeAsync(deleted));
        Assert.Contains("1 công thức", await deleted.Content.ReadAsStringAsync());
    }

    private HttpClient CreateClient(string role)
    {
        var client = factory.CreateClient();
        var issuer = factory.Services.GetRequiredService<IAccessTokenIssuer>();
        var user = new UserAccount(Guid.NewGuid(), "category-test@example.com", "category-test", "Category Test",
            null, true, DateTimeOffset.UtcNow, [role]);
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", issuer.Issue(user).Value);
        return client;
    }

    private sealed record CategoryResponse(Guid Id, int OrderIndex);
}
