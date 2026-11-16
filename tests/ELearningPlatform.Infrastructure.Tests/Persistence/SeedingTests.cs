using ELearningPlatform.Infrastructure.Persistence.Seeding;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ELearningPlatform.Infrastructure.Tests.Persistence;

public sealed class SeedingTests : IDisposable
{
    private static readonly DateTimeOffset Now = new(2026, 5, 10, 9, 0, 0, TimeSpan.Zero);
    private readonly SqliteDatabase _db = new(create: false);
    private readonly string _root = TestFiles.NewTempDirectory();

    public void Dispose()
    {
        _db.Dispose();
        Directory.Delete(_root, recursive: true);
    }

    private ServiceProvider BuildProvider(SeedSettings seed)
    {
        var env = Substitute.For<IWebHostEnvironment>();
        env.WebRootPath.Returns(Path.Combine(_root, "wwwroot"));
        env.ContentRootPath.Returns(_root);

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDbContext<AppDbContext>(o => o.UseSqlite(_db.Connection));
        services.AddIdentityCore<ApplicationUser>(o =>
            {
                o.Password.RequiredLength = 8;
                o.Password.RequireDigit = true;
                o.Password.RequireUppercase = true;
                o.Password.RequireLowercase = true;
                o.Password.RequireNonAlphanumeric = true;
            })
            .AddRoles<IdentityRole>()
            .AddEntityFrameworkStores<AppDbContext>();
        services.AddSingleton(Options.Create(seed));
        services.AddSingleton<TimeProvider>(new FakeTimeProvider(Now));
        services.AddSingleton(env);
        return services.BuildServiceProvider();
    }

    private async Task<AsyncServiceScope> MigratedScopeAsync(SeedSettings seed)
    {
        var scope = BuildProvider(seed).CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.MigrateAsync();
        return scope;
    }

    [Fact]
    public async Task AppDbSeeder_CreatesThreeRoles()
    {
        await using var scope = await MigratedScopeAsync(TestSettings.Seed());

        await AppDbSeeder.SeedAsync(scope.ServiceProvider);

        var roles = await scope.ServiceProvider.GetRequiredService<AppDbContext>().Roles.Select(r => r.Name).OrderBy(n => n).ToListAsync();
        Assert.Equal([RolesConstants.Admin, RolesConstants.Student, RolesConstants.Teacher], roles);
    }

    [Fact]
    public async Task AppDbSeeder_CreatesAdminFromSettings()
    {
        await using var scope = await MigratedScopeAsync(TestSettings.Seed());
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        await AppDbSeeder.SeedAsync(scope.ServiceProvider);

        var admin = await users.FindByEmailAsync("admin@elearning.test");
        Assert.NotNull(admin);
        Assert.True(admin.EmailConfirmed);
        Assert.True(await users.IsInRoleAsync(admin, RolesConstants.Admin));
        Assert.True(await users.CheckPasswordAsync(admin, "Admin@12345"));
    }

    [Fact]
    public async Task AppDbSeeder_IsIdempotent()
    {
        await using var scope = await MigratedScopeAsync(TestSettings.Seed());

        await AppDbSeeder.SeedAsync(scope.ServiceProvider);
        await AppDbSeeder.SeedAsync(scope.ServiceProvider);

        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal(1, await context.Users.CountAsync());
        Assert.Equal(3, await context.Roles.CountAsync());
    }

    [Fact]
    public async Task AppDbSeeder_DoesNotChangeExistingAdminPassword()
    {
        await using (var first = await MigratedScopeAsync(TestSettings.Seed("Admin@12345")))
            await AppDbSeeder.SeedAsync(first.ServiceProvider);

        await using var second = BuildProvider(TestSettings.Seed("Other@99999")).CreateAsyncScope();
        await AppDbSeeder.SeedAsync(second.ServiceProvider);

        var users = second.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var admin = (await users.FindByEmailAsync("admin@elearning.test"))!;
        Assert.True(await users.CheckPasswordAsync(admin, "Admin@12345"));
    }

    [Theory]
    [InlineData("short")]
    [InlineData("alllowercase1!")]
    [InlineData("NoDigitsHere!")]
    [InlineData("NoSymbol123")]
    public async Task AppDbSeeder_WeakAdminPassword_Throws(string password)
    {
        await using var scope = await MigratedScopeAsync(TestSettings.Seed(password));

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => AppDbSeeder.SeedAsync(scope.ServiceProvider));
        Assert.Contains("admin", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public async Task DevDataSeeder_WithoutPassword_Throws(string? password)
    {
        await using var scope = await MigratedScopeAsync(TestSettings.Seed(devPassword: password!));
        await AppDbSeeder.SeedAsync(scope.ServiceProvider);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => DevDataSeeder.SeedAsync(scope.ServiceProvider));
        Assert.Contains("DevUsersPassword", ex.Message);
    }

    [Fact]
    public async Task DevDataSeeder_SeedsUsersCoursesAndExams()
    {
        await using var scope = await MigratedScopeAsync(TestSettings.Seed());
        await AppDbSeeder.SeedAsync(scope.ServiceProvider);

        await DevDataSeeder.SeedAsync(scope.ServiceProvider);

        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal(3, await context.Teachers.CountAsync());
        Assert.Equal(5, await context.Students.CountAsync());
        Assert.Equal(5, await context.Courses.CountAsync());
        Assert.Equal(3, await context.Exams.CountAsync());
        Assert.True(await context.Videos.AnyAsync());
        Assert.True(await context.StudentExams.AnyAsync());
    }

    [Fact]
    public async Task DevDataSeeder_UsersCanSignInWithConfiguredPassword()
    {
        await using var scope = await MigratedScopeAsync(TestSettings.Seed(devPassword: "Dev@Pass123"));
        await AppDbSeeder.SeedAsync(scope.ServiceProvider);
        await DevDataSeeder.SeedAsync(scope.ServiceProvider);

        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var teacher = (await users.FindByEmailAsync("ivan.petrov@elearning.com"))!;
        Assert.True(await users.CheckPasswordAsync(teacher, "Dev@Pass123"));
        Assert.True(await users.IsInRoleAsync(teacher, RolesConstants.Teacher));
    }

    [Fact]
    public async Task DevDataSeeder_UsesTimeProviderForDates()
    {
        await using var scope = await MigratedScopeAsync(TestSettings.Seed());
        await AppDbSeeder.SeedAsync(scope.ServiceProvider);
        await DevDataSeeder.SeedAsync(scope.ServiceProvider);

        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var now = Now.UtcDateTime;
        Assert.All(await context.Courses.ToListAsync(), c => Assert.True(c.CreateAt <= now && c.CreateAt > now.AddDays(-61)));
        Assert.All(await context.CourseReactions.ToListAsync(), r => Assert.Equal(now.AddDays(-2), r.ReactedAt));
        Assert.All(await context.CourseComments.ToListAsync(), c => Assert.Equal(now.AddDays(-2), c.CreatedAt));
        Assert.All(await context.VideoComments.ToListAsync(), c => Assert.Equal(now.AddDays(-1), c.CreatedAt));
    }

    [Fact]
    public async Task DevDataSeeder_PricesAreDecimal()
    {
        await using var scope = await MigratedScopeAsync(TestSettings.Seed());
        await AppDbSeeder.SeedAsync(scope.ServiceProvider);
        await DevDataSeeder.SeedAsync(scope.ServiceProvider);

        var prices = await scope.ServiceProvider.GetRequiredService<AppDbContext>().Courses.Select(c => c.Price).ToListAsync();
        Assert.Contains(49.99m, prices);
        Assert.Contains(0m, prices);
    }

    [Fact]
    public async Task DevDataSeeder_WritesPlaceholderVideoFiles()
    {
        await using var scope = await MigratedScopeAsync(TestSettings.Seed());
        await AppDbSeeder.SeedAsync(scope.ServiceProvider);
        await DevDataSeeder.SeedAsync(scope.ServiceProvider);

        var videos = await scope.ServiceProvider.GetRequiredService<AppDbContext>().Videos.ToListAsync();
        var folder = Path.Combine(_root, "wwwroot", FolderConstants.UploadsFolder, FolderConstants.VideosFolder);
        Assert.All(videos, v => Assert.True(File.Exists(Path.Combine(folder, v.VideoUrl))));
    }

    [Fact]
    public async Task DevDataSeeder_RunsOnlyOnce()
    {
        await using var scope = await MigratedScopeAsync(TestSettings.Seed());
        await AppDbSeeder.SeedAsync(scope.ServiceProvider);
        await DevDataSeeder.SeedAsync(scope.ServiceProvider);

        await DevDataSeeder.SeedAsync(scope.ServiceProvider);

        Assert.Equal(5, await scope.ServiceProvider.GetRequiredService<AppDbContext>().Courses.CountAsync());
    }

    [Fact]
    public async Task InitializeDatabase_MigratesAndSeedsWithoutTestData()
    {
        await using var provider = BuildProvider(TestSettings.Seed());

        await provider.InitializeDatabaseAsync(seedTestData: false);

        await using var scope = provider.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Empty(await context.Database.GetPendingMigrationsAsync());
        Assert.Equal(1, await context.Users.CountAsync());
        Assert.Equal(0, await context.Courses.CountAsync());
    }

    [Fact]
    public async Task InitializeDatabase_WithTestData_SeedsCourses()
    {
        await using var provider = BuildProvider(TestSettings.Seed());

        await provider.InitializeDatabaseAsync(seedTestData: true);

        await using var scope = provider.CreateAsyncScope();
        Assert.Equal(5, await scope.ServiceProvider.GetRequiredService<AppDbContext>().Courses.CountAsync());
    }
}
