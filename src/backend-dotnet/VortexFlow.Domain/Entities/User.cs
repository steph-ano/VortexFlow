using Microsoft.AspNetCore.Identity;

namespace VortexFlow.Domain.Entities;

public class User : IdentityUser
{
    public string Name { get; set; } = string.Empty;
    public string? TenantId { get; set; }
}
