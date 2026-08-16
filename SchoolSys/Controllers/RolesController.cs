using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SchoolSys.Data;
using SchoolSys.Models;
using SchoolSys.Security;
using SchoolSys.Services;
using SchoolSys.ViewModels;
using System.Security.Claims;

// اسم الإجراء Permissions يحجب اسم الصنف، لذا نستخدم اسماً بديلاً
using Perms = SchoolSys.Security.Permissions;

namespace SchoolSys.Controllers;

[HasPermission(Perms.RolesView)]
public class RolesController : BaseController
{
    private readonly ApplicationDbContext _db;
    private readonly RoleManager<ApplicationRole> _roles;
    private readonly IAuditService _audit;

    public RolesController(ApplicationDbContext db, RoleManager<ApplicationRole> roles, IAuditService audit)
    {
        _db = db;
        _roles = roles;
        _audit = audit;
    }

    public async Task<IActionResult> Index()
    {
        var rows = await _db.Roles.AsNoTracking()
            .Select(r => new RoleRow
            {
                Id = r.Id,
                Name = r.Name!,
                DisplayName = r.DisplayName ?? r.Name!,
                Description = r.Description,
                IsSystemRole = r.IsSystemRole,
                UsersCount = _db.UserRoles.Count(ur => ur.RoleId == r.Id),
                PermissionsCount = _db.RoleClaims.Count(c => c.RoleId == r.Id && c.ClaimType == Perms.ClaimType)
            })
            .OrderByDescending(r => r.IsSystemRole).ThenBy(r => r.Name)
            .ToListAsync();

        return View(new RoleIndexViewModel { Roles = rows });
    }

    /// <summary>شاشة تحرير صلاحيات الدور.</summary>
    public async Task<IActionResult> Permissions(int id)
    {
        var role = await _roles.FindByIdAsync(id.ToString());
        if (role is null) return NotFound();

        var granted = await _db.RoleClaims
            .Where(c => c.RoleId == id && c.ClaimType == Perms.ClaimType)
            .Select(c => c.ClaimValue!)
            .ToListAsync();

        return View(new RolePermissionsViewModel
        {
            RoleId = role.Id,
            RoleName = role.Name!,
            DisplayName = role.DisplayName ?? role.Name!,
            Description = role.Description,
            IsSystemRole = role.IsSystemRole,
            UsersCount = await _db.UserRoles.CountAsync(ur => ur.RoleId == id),
            Granted = granted.ToHashSet(StringComparer.OrdinalIgnoreCase)
        });
    }

    [HttpPost, ValidateAntiForgeryToken, HasPermission(Perms.RolesManage)]
    public async Task<IActionResult> Permissions(int roleId, List<string> permissions)
    {
        var role = await _roles.FindByIdAsync(roleId.ToString());
        if (role is null) return NotFound();

        if (role.Name == RoleNames.SuperAdmin)
        {
            Warning("دور مسؤول النظام يملك جميع الصلاحيات دائماً ولا يمكن تعديله.");
            return RedirectToAction(nameof(Permissions), new { id = roleId });
        }

        permissions ??= [];
        var valid = permissions
            .Where(p => Perms.AllPermissions.Contains(p, StringComparer.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var existing = await _db.RoleClaims
            .Where(c => c.RoleId == roleId && c.ClaimType == Perms.ClaimType)
            .ToListAsync();

        var existingValues = existing.Select(c => c.ClaimValue!).ToHashSet(StringComparer.OrdinalIgnoreCase);

        // إزالة ما لم يعد محدداً
        foreach (var claim in existing.Where(c => !valid.Contains(c.ClaimValue!, StringComparer.OrdinalIgnoreCase)))
            await _roles.RemoveClaimAsync(role, new Claim(Perms.ClaimType, claim.ClaimValue!));

        // إضافة الصلاحيات الجديدة
        foreach (var p in valid.Where(p => !existingValues.Contains(p)))
            await _roles.AddClaimAsync(role, new Claim(Perms.ClaimType, p));

        await _audit.LogAsync("تعديل صلاحيات دور", nameof(ApplicationRole), roleId,
            $"{role.Name}: {valid.Count} صلاحية");

        Success($"تم حفظ صلاحيات دور «{role.DisplayName ?? role.Name}» — {valid.Count} صلاحية.");
        return RedirectToAction(nameof(Permissions), new { id = roleId });
    }

    [HttpPost, ValidateAntiForgeryToken, HasPermission(Perms.RolesManage)]
    public async Task<IActionResult> Save(int id, string name, string displayName, string? description)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            Error("الرجاء إدخال اسم الدور.");
            return RedirectToAction(nameof(Index));
        }

        name = name.Trim();

        if (id == 0)
        {
            if (await _roles.RoleExistsAsync(name))
            {
                Error("يوجد دور بنفس الاسم.");
                return RedirectToAction(nameof(Index));
            }

            var result = await _roles.CreateAsync(new ApplicationRole
            {
                Name = name,
                DisplayName = string.IsNullOrWhiteSpace(displayName) ? name : displayName.Trim(),
                Description = description,
                IsSystemRole = false
            });

            if (!result.Succeeded)
            {
                Error(string.Join("، ", result.Errors.Select(e => e.Description)));
                return RedirectToAction(nameof(Index));
            }

            Success("تم إنشاء الدور. يمكنك الآن تحديد صلاحياته.");
        }
        else
        {
            var role = await _roles.FindByIdAsync(id.ToString());
            if (role is null) return NotFound();

            role.DisplayName = string.IsNullOrWhiteSpace(displayName) ? role.Name : displayName.Trim();
            role.Description = description;

            if (!role.IsSystemRole) role.Name = name;

            await _roles.UpdateAsync(role);
            Success("تم حفظ بيانات الدور.");
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken, HasPermission(Perms.RolesManage)]
    public async Task<IActionResult> Delete(int id)
    {
        var role = await _roles.FindByIdAsync(id.ToString());
        if (role is null) return NotFound();

        if (role.IsSystemRole)
        {
            Error("لا يمكن حذف أدوار النظام الأساسية.");
            return RedirectToAction(nameof(Index));
        }

        var usersCount = await _db.UserRoles.CountAsync(ur => ur.RoleId == id);
        if (usersCount > 0)
        {
            Error($"لا يمكن حذف الدور لارتباطه بـ {usersCount} مستخدم.");
            return RedirectToAction(nameof(Index));
        }

        await _roles.DeleteAsync(role);
        await _audit.LogAsync("حذف دور", nameof(ApplicationRole), id, role.Name);

        Success("تم حذف الدور.");
        return RedirectToAction(nameof(Index));
    }
}
