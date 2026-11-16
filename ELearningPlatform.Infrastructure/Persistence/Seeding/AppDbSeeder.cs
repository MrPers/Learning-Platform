using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace ELearningPlatform.Infrastructure.Persistence.Seeding;

public static class AppDbSeeder
{
    /// <summary>Creates the roles and the admin account. Admin credentials come from the "Seed" config section.</summary>
    public static async Task SeedAsync(IServiceProvider serviceProvider)
    {
        var roleManager = serviceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        var userManager = serviceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var seed = serviceProvider.GetRequiredService<IOptions<SeedSettings>>().Value;

        foreach (var role in new[] { RolesConstants.Admin, RolesConstants.Teacher, RolesConstants.Student })
        {
            if (!await roleManager.RoleExistsAsync(role))
                await roleManager.CreateAsync(new IdentityRole(role));
        }

        if (await userManager.FindByEmailAsync(seed.AdminEmail) != null)
            return;

        var admin = new ApplicationUser
        {
            UserName = "admin",
            FirstName = "Admin",
            LastName = "1",
            Gender = Gender.Male,
            Email = seed.AdminEmail,
            EmailConfirmed = true,
        };

        var result = await userManager.CreateAsync(admin, seed.AdminPassword);
        if (!result.Succeeded)
            throw new InvalidOperationException($"Could not create the admin account: {result.ErrorText()}");

        await userManager.AddToRoleAsync(admin, RolesConstants.Admin);
    }
}
