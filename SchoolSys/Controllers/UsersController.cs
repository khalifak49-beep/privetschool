using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SchoolSys.Data;
using SchoolSys.Models;
using SchoolSys.Security;
using SchoolSys.Services;
using SchoolSys.ViewModels;

namespace SchoolSys.Controllers;

[HasPermission(Permissions.UsersView)]
public class UsersController : BaseController
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _users;
    private readonly RoleManager<ApplicationRole> _roles;
    private readonly IAuditService _audit;
    private readonly ICurrentUserService _current;

    public UsersController(ApplicationDbContext db, UserManager<ApplicationUser> users,
        RoleManager<ApplicationRole> roles, IAuditService audit, ICurrentUserService current)
    {
        _db = db;
        _users = users;
        _roles = roles;
        _audit = audit;
        _current = current;
    }

    public async Task<IActionResult> Index(string? q, string? role, bool? isActive, int page = 1)
    {
        var vm = new UserIndexViewModel { Q = q, Role = role, IsActive = isActive, Page = page };

        var query = _db.Users.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(q))
        {
            var term = q.Trim();
            query = query.Where(u => u.FullName.Contains(term) ||
                                     (u.Email != null && u.Email.Contains(term)) ||
                                     (u.PhoneNumber != null && u.PhoneNumber.Contains(term)));
        }

        if (isActive.HasValue) query = query.Where(u => u.IsActive == isActive);

        if (!string.IsNullOrEmpty(role))
        {
            var roleId = await _db.Roles.Where(r => r.Name == role).Select(r => r.Id).FirstOrDefaultAsync();
            query = query.Where(u => _db.UserRoles.Any(ur => ur.UserId == u.Id && ur.RoleId == roleId));
        }

        var projected = query.Select(u => new UserRow
        {
            Id = u.Id,
            FullName = u.FullName,
            Email = u.Email ?? "",
            PhoneNumber = u.PhoneNumber,
            PhotoPath = u.PhotoPath,
            IsActive = u.IsActive,
            CreatedAt = u.CreatedAt,
            LastLoginAt = u.LastLoginAt,
            IsLockedOut = u.LockoutEnd != null && u.LockoutEnd > DateTimeOffset.Now,
            Roles = (from ur in _db.UserRoles
                     join r in _db.Roles on ur.RoleId equals r.Id
                     where ur.UserId == u.Id
                     select r.Name!).ToList(),
            LinkedTo = u.StudentId != null ? "طالب"
                : u.GuardianId != null ? "ولي أمر"
                : u.EmployeeId != null ? "موظف" : null
        });

        vm.Users = await PagedList<UserRow>.CreateAsync(projected.OrderBy(u => u.FullName), page, 25);
        vm.TotalUsers = await _db.Users.CountAsync();
        vm.ActiveUsers = await _db.Users.CountAsync(u => u.IsActive);
        vm.Roles = await _db.Roles.OrderBy(r => r.Name).Select(r => r.Name!).ToListAsync();

        return View(vm);
    }

    [HasPermission(Permissions.UsersManage)]
    public async Task<IActionResult> Create()
    {
        var vm = new UserFormViewModel
        {
            AllRoles = await _db.Roles.OrderBy(r => r.Name).Select(r => r.Name!).ToListAsync()
        };
        return View("Form", vm);
    }

    [HttpPost, ValidateAntiForgeryToken, HasPermission(Permissions.UsersManage)]
    public async Task<IActionResult> Create(UserFormViewModel vm)
    {
        if (string.IsNullOrWhiteSpace(vm.Password))
            ModelState.AddModelError(nameof(vm.Password), "الرجاء إدخال كلمة المرور.");

        if (!ModelState.IsValid)
        {
            vm.AllRoles = await _db.Roles.OrderBy(r => r.Name).Select(r => r.Name!).ToListAsync();
            return View("Form", vm);
        }

        var user = new ApplicationUser
        {
            UserName = vm.Email,
            Email = vm.Email,
            EmailConfirmed = true,
            FullName = vm.FullName.Trim(),
            PhoneNumber = vm.PhoneNumber,
            IsActive = vm.IsActive
        };

        var result = await _users.CreateAsync(user, vm.Password!);
        if (!result.Succeeded)
        {
            foreach (var e in result.Errors)
                ModelState.AddModelError(string.Empty, AccountController.TranslateIdentityError(e));
            vm.AllRoles = await _db.Roles.OrderBy(r => r.Name).Select(r => r.Name!).ToListAsync();
            return View("Form", vm);
        }

        if (vm.SelectedRoles.Count > 0)
            await _users.AddToRolesAsync(user, vm.SelectedRoles);

        await _audit.LogAsync("إنشاء مستخدم", nameof(ApplicationUser), user.Id, vm.Email);
        Success($"تم إنشاء المستخدم «{vm.FullName}».");
        return RedirectToAction(nameof(Index));
    }

    [HasPermission(Permissions.UsersManage)]
    public async Task<IActionResult> Edit(int id)
    {
        var user = await _users.FindByIdAsync(id.ToString());
        if (user is null) return NotFound();

        var vm = new UserFormViewModel
        {
            Id = user.Id,
            FullName = user.FullName,
            Email = user.Email ?? "",
            PhoneNumber = user.PhoneNumber,
            IsActive = user.IsActive,
            SelectedRoles = (await _users.GetRolesAsync(user)).ToList(),
            AllRoles = await _db.Roles.OrderBy(r => r.Name).Select(r => r.Name!).ToListAsync(),
            LinkedTo = user.StudentId != null ? "حساب طالب"
                : user.GuardianId != null ? "حساب ولي أمر"
                : user.EmployeeId != null ? "حساب موظف" : null
        };

        return View("Form", vm);
    }

    [HttpPost, ValidateAntiForgeryToken, HasPermission(Permissions.UsersManage)]
    public async Task<IActionResult> Edit(UserFormViewModel vm)
    {
        if (!ModelState.IsValid)
        {
            vm.AllRoles = await _db.Roles.OrderBy(r => r.Name).Select(r => r.Name!).ToListAsync();
            return View("Form", vm);
        }

        var user = await _users.FindByIdAsync(vm.Id.ToString());
        if (user is null) return NotFound();

        // منع المستخدم من تعطيل حسابه أو سحب دور مسؤول النظام من نفسه
        var isSelf = _current.UserId == user.Id;
        if (isSelf && !vm.IsActive)
        {
            Error("لا يمكنك تعطيل حسابك الشخصي.");
            vm.AllRoles = await _db.Roles.OrderBy(r => r.Name).Select(r => r.Name!).ToListAsync();
            return View("Form", vm);
        }

        user.FullName = vm.FullName.Trim();
        user.PhoneNumber = vm.PhoneNumber;
        user.IsActive = vm.IsActive;

        if (!string.Equals(user.Email, vm.Email, StringComparison.OrdinalIgnoreCase))
        {
            user.Email = vm.Email;
            user.UserName = vm.Email;
        }

        var updateResult = await _users.UpdateAsync(user);
        if (!updateResult.Succeeded)
        {
            foreach (var e in updateResult.Errors)
                ModelState.AddModelError(string.Empty, AccountController.TranslateIdentityError(e));
            vm.AllRoles = await _db.Roles.OrderBy(r => r.Name).Select(r => r.Name!).ToListAsync();
            return View("Form", vm);
        }

        // تحديث الأدوار
        var currentRoles = await _users.GetRolesAsync(user);
        var toRemove = currentRoles.Except(vm.SelectedRoles).ToList();
        var toAdd = vm.SelectedRoles.Except(currentRoles).ToList();

        if (isSelf && toRemove.Contains(RoleNames.SuperAdmin))
        {
            Error("لا يمكنك إزالة دور مسؤول النظام من حسابك.");
            return RedirectToAction(nameof(Edit), new { id = vm.Id });
        }

        if (toRemove.Count > 0) await _users.RemoveFromRolesAsync(user, toRemove);
        if (toAdd.Count > 0) await _users.AddToRolesAsync(user, toAdd);

        // تغيير كلمة المرور اختيارياً
        if (!string.IsNullOrWhiteSpace(vm.Password))
        {
            var token = await _users.GeneratePasswordResetTokenAsync(user);
            var reset = await _users.ResetPasswordAsync(user, token, vm.Password);
            if (!reset.Succeeded)
            {
                foreach (var e in reset.Errors)
                    Error(AccountController.TranslateIdentityError(e));
            }
            else
            {
                Info("تم تغيير كلمة المرور.");
            }
        }

        await _audit.LogAsync("تعديل مستخدم", nameof(ApplicationUser), user.Id, vm.Email);
        Success("تم حفظ بيانات المستخدم.");
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken, HasPermission(Permissions.UsersManage)]
    public async Task<IActionResult> ToggleActive(int id)
    {
        var user = await _users.FindByIdAsync(id.ToString());
        if (user is null) return NotFound();

        if (_current.UserId == user.Id)
        {
            Error("لا يمكنك تعطيل حسابك الشخصي.");
            return RedirectToAction(nameof(Index));
        }

        user.IsActive = !user.IsActive;
        await _users.UpdateAsync(user);
        await _audit.LogAsync(user.IsActive ? "تفعيل مستخدم" : "تعطيل مستخدم",
            nameof(ApplicationUser), user.Id, user.Email);

        Success(user.IsActive ? "تم تفعيل الحساب." : "تم تعطيل الحساب.");
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken, HasPermission(Permissions.UsersManage)]
    public async Task<IActionResult> Unlock(int id)
    {
        var user = await _users.FindByIdAsync(id.ToString());
        if (user is null) return NotFound();

        await _users.SetLockoutEndDateAsync(user, null);
        await _users.ResetAccessFailedCountAsync(user);

        Success("تم إلغاء قفل الحساب.");
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken, HasPermission(Permissions.UsersManage)]
    public async Task<IActionResult> Delete(int id)
    {
        var user = await _users.FindByIdAsync(id.ToString());
        if (user is null) return NotFound();

        if (_current.UserId == user.Id)
        {
            Error("لا يمكنك حذف حسابك الشخصي.");
            return RedirectToAction(nameof(Index));
        }

        // آخر مسؤول نظام لا يُحذف
        if (await _users.IsInRoleAsync(user, RoleNames.SuperAdmin))
        {
            var admins = await _users.GetUsersInRoleAsync(RoleNames.SuperAdmin);
            if (admins.Count <= 1)
            {
                Error("لا يمكن حذف آخر حساب لمسؤول النظام.");
                return RedirectToAction(nameof(Index));
            }
        }

        var email = user.Email;
        var result = await _users.DeleteAsync(user);

        if (!result.Succeeded)
        {
            Error("تعذّر حذف المستخدم لارتباطه بسجلات أخرى. يمكنك تعطيله بدلاً من ذلك.");
            return RedirectToAction(nameof(Index));
        }

        await _audit.LogAsync("حذف مستخدم", nameof(ApplicationUser), id, email);
        Success("تم حذف المستخدم.");
        return RedirectToAction(nameof(Index));
    }
}
