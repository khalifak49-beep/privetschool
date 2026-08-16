using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;

namespace SchoolSys.Security;

/// <summary>متطلب صلاحية واحدة.</summary>
public class PermissionRequirement : IAuthorizationRequirement
{
    public string Permission { get; }
    public PermissionRequirement(string permission) => Permission = permission;
}

/// <summary>
/// يتحقق من وجود claim الصلاحية لدى المستخدم. مسؤول النظام (SuperAdmin) يتجاوز كل الفحوصات.
/// </summary>
public class PermissionAuthorizationHandler : AuthorizationHandler<PermissionRequirement>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context, PermissionRequirement requirement)
    {
        if (context.User.Identity?.IsAuthenticated != true)
            return Task.CompletedTask;

        if (context.User.IsInRole(RoleNames.SuperAdmin))
        {
            context.Succeed(requirement);
            return Task.CompletedTask;
        }

        var granted = context.User.Claims.Any(c =>
            c.Type == Permissions.ClaimType &&
            string.Equals(c.Value, requirement.Permission, StringComparison.OrdinalIgnoreCase));

        if (granted) context.Succeed(requirement);
        return Task.CompletedTask;
    }
}

/// <summary>
/// يولّد سياسات الصلاحيات ديناميكياً بدل تسجيل عشرات السياسات يدوياً.
/// </summary>
public class PermissionPolicyProvider : DefaultAuthorizationPolicyProvider
{
    public const string PolicyPrefix = "permission:";

    public PermissionPolicyProvider(IOptions<AuthorizationOptions> options) : base(options) { }

    public override async Task<AuthorizationPolicy?> GetPolicyAsync(string policyName)
    {
        if (policyName.StartsWith(PolicyPrefix, StringComparison.OrdinalIgnoreCase))
        {
            var permission = policyName[PolicyPrefix.Length..];
            return new AuthorizationPolicyBuilder()
                .AddRequirements(new PermissionRequirement(permission))
                .Build();
        }

        return await base.GetPolicyAsync(policyName);
    }
}

/// <summary>[HasPermission(Permissions.StudentsView)] على الـ Controller أو الـ Action.</summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
public class HasPermissionAttribute : AuthorizeAttribute
{
    public HasPermissionAttribute(string permission)
        => Policy = PermissionPolicyProvider.PolicyPrefix + permission;
}
