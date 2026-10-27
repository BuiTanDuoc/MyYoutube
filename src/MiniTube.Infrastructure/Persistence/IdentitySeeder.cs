using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MiniTube.Domain.Constants;
using MiniTube.Domain.Entities;

namespace MiniTube.Infrastructure.Persistence;

public static class IdentitySeeder
{
    public static async Task SeedAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var sp = scope.ServiceProvider;
        var roleManager = sp.GetRequiredService<RoleManager<ApplicationRole>>();
        var userManager = sp.GetRequiredService<UserManager<ApplicationUser>>();
        var config = sp.GetRequiredService<IConfiguration>();
        var logger = sp.GetRequiredService<ILoggerFactory>().CreateLogger("IdentitySeeder");

        foreach (var role in Roles.All)
        {
            if (!await roleManager.RoleExistsAsync(role))
                await roleManager.CreateAsync(new ApplicationRole(role));
        }

        var email = config["Seed:Admin:Email"];
        var userName = config["Seed:Admin:UserName"];
        var password = config["Seed:Admin:Password"];
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(userName) || string.IsNullOrWhiteSpace(password))
            return;

        if (await userManager.FindByEmailAsync(email) is not null) return;

        var admin = new ApplicationUser
        {
            UserName = userName,
            Email = email,
            EmailConfirmed = true,
            DisplayName = "Administrator"
        };

        var result = await userManager.CreateAsync(admin, password);
        if (!result.Succeeded)
        {
            logger.LogError("Failed to seed admin: {Errors}", string.Join("; ", result.Errors.Select(e => e.Description)));
            return;
        }

        await userManager.AddToRolesAsync(admin, [Roles.Admin, Roles.User]);
        logger.LogInformation("Seeded admin user {Email}", email);
    }
}
