using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using SchoolSys.Models;

namespace SchoolSys.Security;

/// <summary>
/// يضيف بيانات إضافية إلى هوية المستخدم (الاسم الكامل، الصورة، الروابط)
/// بالإضافة إلى صلاحيات الأدوار التي يضيفها المصنع الأساسي تلقائياً.
/// </summary>
public class AppClaimsPrincipalFactory
    : UserClaimsPrincipalFactory<ApplicationUser, ApplicationRole>
{
    public AppClaimsPrincipalFactory(
        UserManager<ApplicationUser> userManager,
        RoleManager<ApplicationRole> roleManager,
        IOptions<IdentityOptions> options)
        : base(userManager, roleManager, options) { }

    protected override async Task<ClaimsIdentity> GenerateClaimsAsync(ApplicationUser user)
    {
        var identity = await base.GenerateClaimsAsync(user);

        identity.AddClaim(new Claim("FullName", user.FullName ?? user.UserName ?? ""));

        if (!string.IsNullOrEmpty(user.PhotoPath))
            identity.AddClaim(new Claim("Photo", user.PhotoPath));

        if (user.EmployeeId.HasValue)
            identity.AddClaim(new Claim("EmployeeId", user.EmployeeId.Value.ToString()));
        if (user.StudentId.HasValue)
            identity.AddClaim(new Claim("StudentId", user.StudentId.Value.ToString()));
        if (user.GuardianId.HasValue)
            identity.AddClaim(new Claim("GuardianId", user.GuardianId.Value.ToString()));

        return identity;
    }
}
