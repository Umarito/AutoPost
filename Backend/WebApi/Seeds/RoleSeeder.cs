// WebApi/Seeds/RoleSeeder.cs
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Threading.Tasks;

namespace WebApi.Seeds;

/// <summary>
/// Facilitates the database seeding of default Identity roles during application bootstrapping.
/// </summary>
/// <remarks>
/// <para><b>Core Definition:</b> Seeds ASP.NET Core Identity roles into PostgreSQL if they do not already exist.</para>
/// <para><b>Business &amp; Technical Justification:</b> Ensures the security schema has the required global roles populated before controllers enforce authorizations.</para>
/// <para><b>Execution, Process &amp; Relationships:</b> Runs asynchronously during startup inside a scoped ServiceProvider container, executing database lookups and inserts.</para>
/// <para><b>Project Impact &amp; Indispensability:</b> Seals application initialization security, preventing authorization failures on first-run environments.</para>
/// </remarks>
public static class RoleSeeder
{
    /// <summary>
    /// Checks and inserts default system roles into the database.
    /// </summary>
    /// <param name="serviceProvider">The root service provider instance.</param>
    /// <returns>A task representing the seeding operation.</returns>
    public static async Task SeedRolesAsync(IServiceProvider serviceProvider)
    {
        using var scope = serviceProvider.CreateScope();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();

        foreach (var roleName in DefaultRoles.AllRoles)
        {
            var exists = await roleManager.RoleExistsAsync(roleName);
            if (!exists)
            {
                var role = new IdentityRole<Guid>
                {
                    Name = roleName,
                    NormalizedName = roleName.ToUpperInvariant()
                };
                await roleManager.CreateAsync(role);
            }
        }
    }
}
