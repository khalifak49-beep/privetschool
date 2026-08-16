using System.Security.Claims;

namespace SchoolSys.Security;

public static class PermissionExtensions
{
    /// <summary>فحص صلاحية داخل الـ Views: @User.Can(Permissions.StudentsView)</summary>
    public static bool Can(this ClaimsPrincipal? user, string permission)
    {
        if (user?.Identity?.IsAuthenticated != true) return false;
        if (user.IsInRole(RoleNames.SuperAdmin)) return true;

        return user.Claims.Any(c => c.Type == Permissions.ClaimType &&
                                    string.Equals(c.Value, permission, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>هل يملك المستخدم أي صلاحية من القائمة (لإظهار مجموعات القائمة الجانبية).</summary>
    public static bool CanAny(this ClaimsPrincipal? user, params string[] permissions)
        => permissions.Any(user.Can);

    public static string DisplayName(this ClaimsPrincipal? user)
        => user?.FindFirst("FullName")?.Value ?? user?.Identity?.Name ?? "مستخدم";

    public static string Initials(this ClaimsPrincipal? user)
    {
        var name = user.DisplayName().Trim();
        if (string.IsNullOrEmpty(name)) return "؟";
        var parts = name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length >= 2 ? $"{parts[0][0]}{parts[1][0]}" : name[..Math.Min(2, name.Length)];
    }

    public static string? Photo(this ClaimsPrincipal? user)
        => user?.FindFirst("Photo")?.Value;

    public static string PrimaryRole(this ClaimsPrincipal? user)
    {
        var role = user?.FindFirst(ClaimTypes.Role)?.Value;
        return role is null ? "" : RoleNames.Display(role);
    }
}
