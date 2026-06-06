using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using VortexFlow.Application.Audit;
using VortexFlow.Domain.Entities;
using VortexFlow.Infrastructure.Data;

namespace VortexFlow.Api.Bootstrap;

/// <summary>
/// One-shot bootstrap executed after the host is built. Creates roles, optionally
/// seeds an admin user, and runs database migrations. Admin seeding is opt-in via
/// <c>Bootstrap:SeedAdmin</c>; in production environments the bootstrap must
/// only run from a controlled init job (a one-off Deployment with the same
/// image), not from the application host on every replica startup.
/// </summary>
public static class DataSeeder
{
    public static async Task RunAsync(IServiceProvider services, IConfiguration config, IWebHostEnvironment env, CancellationToken ct = default)
    {
        await using var scope = services.CreateAsyncScope();
        var sp = scope.ServiceProvider;
        var logger = sp.GetRequiredService<ILoggerFactory>().CreateLogger("DataSeeder");

        var context = sp.GetRequiredService<VortexFlowDbContext>();
        await context.Database.MigrateAsync(ct);

        var roleManager = sp.GetRequiredService<RoleManager<IdentityRole>>();
        foreach (var role in new[] { "Admin", "Editor", "Viewer", "Internal" })
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                var result = await roleManager.CreateAsync(new IdentityRole(role));
                if (!result.Succeeded)
                {
                    logger.LogError("Failed creating role {Role}: {Errors}", role, string.Join(",", result.Errors.Select(e => e.Description)));
                }
            }
        }

        var seedAdmin = config.GetValue("Bootstrap:SeedAdmin", false);
        if (!seedAdmin)
        {
            logger.LogInformation("Bootstrap:SeedAdmin=false; skipping admin user seed.");
            return;
        }

        if (!env.IsDevelopment())
        {
            logger.LogWarning(
                "Bootstrap:SeedAdmin=true in non-Development environment '{Env}'. " +
                "This is only safe when the API is deployed as a one-shot init job.",
                env.EnvironmentName);
        }

        var email = config["Bootstrap:AdminEmail"];
        var password = config["Bootstrap:AdminPassword"];
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            logger.LogError("Bootstrap:SeedAdmin=true but AdminEmail or AdminPassword is missing. Skipping.");
            return;
        }
        if (password.Length < 12)
        {
            logger.LogError("Bootstrap admin password is too short; must be >= 12 characters. Skipping.");
            return;
        }

        var userManager = sp.GetRequiredService<UserManager<User>>();
        var adminUser = await userManager.FindByEmailAsync(email);
        if (adminUser is null)
        {
            adminUser = new User { UserName = email, Email = email, Name = "System Admin" };
            var createResult = await userManager.CreateAsync(adminUser, password);
            if (!createResult.Succeeded)
            {
                logger.LogError("Failed creating admin user: {Errors}", string.Join(",", createResult.Errors.Select(e => e.Description)));
                return;
            }
        }
        if (!await userManager.IsInRoleAsync(adminUser, "Admin"))
        {
            await userManager.AddToRoleAsync(adminUser, "Admin");
        }

        var auditLogger = sp.GetRequiredService<ISecurityAuditLogger>();
        auditLogger.AdminAction(adminUser.Id, "bootstrap_admin_seeded", adminUser.Id, null);
    }
}
