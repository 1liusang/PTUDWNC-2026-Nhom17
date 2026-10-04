using System.Linq.Expressions;
using CulinaryBlog.Application.Abstractions;
using CulinaryBlog.Application.Common.Exceptions;
using CulinaryBlog.Application.Features.RecipeImages;
using CulinaryBlog.Application.Features.Recipes;
using CulinaryBlog.Domain.Common;
using CulinaryBlog.Domain.Entities;

namespace CulinaryBlog.UnitTests.RecipeImages;

public sealed class RecipeImageCommandTests
{
    private static readonly byte[] Jpeg = [0xFF, 0xD8, 0xFF, 0xE0];

    [Fact]
    public async Task Upload_ValidJpeg_CreatesPrimaryImageAndQueuesResize()
    {
        var fixture = new Fixture();
        using var file = new MemoryStream(Jpeg);

        var result = await fixture.Upload.Handle(
            new UploadRecipeImageCommand(fixture.Recipe.Id, file, "image/jpeg", " Món ngon "), CancellationToken.None);

        Assert.True(result.IsPrimary);
        Assert.Equal("Món ngon", result.AltText);
        Assert.EndsWith("/original.jpg", fixture.Storage.UploadedKey);
        Assert.Equal("image/jpeg", fixture.Storage.UploadedContentType);
        Assert.NotNull(fixture.Recipe.UpdatedAt);
        Assert.Single(fixture.Db.Images);
        Assert.Contains(nameof(IRecipeImageResizeJob), fixture.Jobs.Enqueued[0]);
    }

    [Fact]
    public async Task Upload_WhenSignatureDoesNotMatchMime_RejectsWithoutStorageWrite()
    {
        var fixture = new Fixture();
        using var file = new MemoryStream(Jpeg);

        var error = await Assert.ThrowsAsync<FileTypeNotAllowedException>(() => fixture.Upload.Handle(
            new UploadRecipeImageCommand(fixture.Recipe.Id, file, "image/png", null), CancellationToken.None));

        Assert.Equal("FILE_TYPE_NOT_ALLOWED", error.Code);
        Assert.Null(fixture.Storage.UploadedKey);
    }

    [Fact]
    public async Task Upload_AvifSignature_IsRejectedByCurrentDecision()
    {
        var fixture = new Fixture();
        using var file = new MemoryStream([0, 0, 0, 20, 0x66, 0x74, 0x79, 0x70, 0x61, 0x76, 0x69, 0x66]);

        await Assert.ThrowsAsync<FileTypeNotAllowedException>(() => fixture.Upload.Handle(
            new UploadRecipeImageCommand(fixture.Recipe.Id, file, "image/avif", null), CancellationToken.None));
    }

    [Fact]
    public async Task Upload_OverFiveMegabytes_RejectsWithoutStorageWrite()
    {
        var fixture = new Fixture();
        var bytes = new byte[5 * 1024 * 1024 + 1];
        Jpeg.CopyTo(bytes, 0);
        using var file = new MemoryStream(bytes);

        var error = await Assert.ThrowsAsync<FileSizeExceededException>(() => fixture.Upload.Handle(
            new UploadRecipeImageCommand(fixture.Recipe.Id, file, "image/jpeg", null), CancellationToken.None));

        Assert.Equal("FILE_SIZE_EXCEEDED", error.Code);
        Assert.Null(fixture.Storage.UploadedKey);
    }

    [Fact]
    public async Task Upload_WhenCallerIsNotOwner_ThrowsForbidden()
    {
        var fixture = new Fixture();
        fixture.CurrentUser.UserId = Guid.NewGuid();
        using var file = new MemoryStream(Jpeg);

        await Assert.ThrowsAsync<ForbiddenException>(() => fixture.Upload.Handle(
            new UploadRecipeImageCommand(fixture.Recipe.Id, file, "image/jpeg", null), CancellationToken.None));

        Assert.Null(fixture.Storage.UploadedKey);
    }

    [Fact]
    public async Task Update_SetPrimary_LeavesExactlyOnePrimary()
    {
        var fixture = new Fixture();
        var first = fixture.AddImage(isPrimary: true, order: 0);
        var second = fixture.AddImage(isPrimary: false, order: 1);

        var result = await fixture.Update.Handle(new UpdateRecipeImageCommand(
            fixture.Recipe.Id, second.Id, false, null, true, 2), CancellationToken.None);

        Assert.False(first.IsPrimary);
        Assert.True(second.IsPrimary);
        Assert.Equal(2, result.OrderIndex);
        Assert.Equal(1, fixture.Db.TransactionCount);
        Assert.Equal(2, fixture.Db.SaveCount);
    }

    [Fact]
    public async Task Update_ExplicitNullAltText_ClearsCaption()
    {
        var fixture = new Fixture();
        var image = fixture.AddImage(isPrimary: true, order: 0);
        image.AltText = "Cũ";

        await fixture.Update.Handle(new UpdateRecipeImageCommand(
            fixture.Recipe.Id, image.Id, true, null, null, null), CancellationToken.None);

        Assert.Null(image.AltText);
        Assert.True(image.IsPrimary);
    }

    [Fact]
    public async Task Delete_Primary_PromotesLowestOrderAndQueuesPrefixDeletion()
    {
        var fixture = new Fixture();
        var first = fixture.AddImage(isPrimary: true, order: 0);
        var later = fixture.AddImage(isPrimary: false, order: 5);
        var next = fixture.AddImage(isPrimary: false, order: 2);

        await fixture.Delete.Handle(new DeleteRecipeImageCommand(fixture.Recipe.Id, first.Id), CancellationToken.None);

        Assert.DoesNotContain(first, fixture.Db.Images);
        Assert.True(next.IsPrimary);
        Assert.False(later.IsPrimary);
        Assert.Equal(1, fixture.Db.TransactionCount);
        Assert.Equal($"recipes/{fixture.Recipe.Id}/{first.Id}", fixture.Jobs.DeletedPrefix);
    }

    private sealed class Fixture
    {
        public TestDb Db { get; } = new();
        public TestStorage Storage { get; } = new();
        public TestJobs Jobs { get; } = new();
        public TestCache Cache { get; } = new();
        public TestCurrentUser CurrentUser { get; } = new() { UserId = Guid.NewGuid() };
        public Recipe Recipe { get; }
        public UploadRecipeImageCommandHandler Upload { get; }
        public UpdateRecipeImageCommandHandler Update { get; }
        public DeleteRecipeImageCommandHandler Delete { get; }

        public Fixture()
        {
            Recipe = new Recipe { Id = Guid.NewGuid(), AuthorId = CurrentUser.UserId!.Value.ToString() };
            Db.RecipesList.Add(Recipe);
            var authorization = new RecipeAuthorizationHandler(CurrentUser);
            Upload = new UploadRecipeImageCommandHandler(Db, Storage, Jobs, authorization, Cache);
            Update = new UpdateRecipeImageCommandHandler(Db, Storage, authorization, Cache);
            Delete = new DeleteRecipeImageCommandHandler(Db, authorization, Jobs, Cache);
        }

        public RecipeImage AddImage(bool isPrimary, int order)
        {
            var image = new RecipeImage
            {
                Id = Guid.NewGuid(), RecipeId = Recipe.Id, IsPrimary = isPrimary,
                OrderIndex = order, OriginalKey = $"recipes/{Recipe.Id}/{Guid.NewGuid()}/original.jpg"
            };
            Db.Images.Add(image);
            return image;
        }
    }

    private sealed class TestCurrentUser : ICurrentUser
    {
        public Guid? UserId { get; set; }
        public string? IpAddress => null;
        public bool IsInRole(string role) => false;
    }

    private sealed class TestDb : IAppDbContext
    {
        public List<Recipe> RecipesList { get; } = [];
        public List<RecipeImage> Images { get; } = [];
        public int SaveCount { get; private set; }
        public int TransactionCount { get; private set; }
        public IQueryable<Category> Categories => Enumerable.Empty<Category>().AsQueryable();
        public IQueryable<Recipe> Recipes => RecipesList.AsQueryable();
        public IQueryable<Recipe> RecipesIncludingDeleted => Recipes;
        public IQueryable<RecipeStep> RecipeSteps => Enumerable.Empty<RecipeStep>().AsQueryable();
        public IQueryable<RecipeIngredient> RecipeIngredients => Enumerable.Empty<RecipeIngredient>().AsQueryable();
        public IQueryable<RecipeImage> RecipeImages => Images.AsQueryable();
        public void Add<TEntity>(TEntity entity) where TEntity : class
        {
            if (entity is RecipeImage image) Images.Add(image);
        }
        public void Remove<TEntity>(TEntity entity) where TEntity : class
        {
            if (entity is RecipeImage image) Images.Remove(image);
        }
        public void SetOriginalVersion<TEntity>(TEntity entity, uint version) where TEntity : BaseEntity { }
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            SaveCount++;
            return Task.FromResult(1);
        }
        public async Task ExecuteInTransactionAsync(Func<Task> action, CancellationToken cancellationToken = default)
        {
            TransactionCount++;
            await action();
        }
    }

    private sealed class TestStorage : IFileStorage
    {
        public string? UploadedKey { get; private set; }
        public string? UploadedContentType { get; private set; }
        public Task UploadAsync(Stream content, string key, string contentType, CancellationToken cancellationToken = default)
        {
            UploadedKey = key;
            UploadedContentType = contentType;
            return Task.CompletedTask;
        }
        public Task DeleteAsync(string key, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task DeleteByPrefixAsync(string prefix, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public string GetPublicUrl(string key) => $"/media/{key}";
    }

    private sealed class TestJobs : IBackgroundJobService
    {
        public List<string> Enqueued { get; } = [];
        public string? DeletedPrefix { get; private set; }
        public string Enqueue<TJob>(Expression<Func<TJob, Task>> methodCall)
        {
            Enqueued.Add($"{typeof(TJob).Name}: {methodCall}");
            if (typeof(TJob) == typeof(IRecipeImageDeleteJob) && methodCall.Body is MethodCallExpression call)
                DeletedPrefix = Expression.Lambda<Func<string>>(call.Arguments[0]).Compile()();
            return "job-1";
        }
        public string Schedule<TJob>(Expression<Func<TJob, Task>> methodCall, TimeSpan delay) => throw new NotSupportedException();
        public void AddOrUpdateRecurring<TJob>(string recurringJobId, Expression<Func<TJob, Task>> methodCall, string cronExpression) => throw new NotSupportedException();
    }

    private sealed class TestCache : ICacheInvalidator
    {
        public Task InvalidateAsync(IEnumerable<string> tags, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
