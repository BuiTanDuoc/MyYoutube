using Microsoft.AspNetCore.Identity;

namespace MiniTube.Domain.Entities;

public class ApplicationRole : IdentityRole<Guid>
{
    public ApplicationRole() { }
    public ApplicationRole(string name) : base(name) { }
}
