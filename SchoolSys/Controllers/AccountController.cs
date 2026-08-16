using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using SchoolSys.Models;
using SchoolSys.Services;
using SchoolSys.ViewModels;

namespace SchoolSys.Controllers;

[Authorize]
public class AccountController : BaseController
{
    private readonly SignInManager<ApplicationUser> _signIn;
    private readonly UserManager<ApplicationUser> _users;
    private readonly IFileStorageService _files;
    private readonly IAuditService _audit;

    public AccountController(SignInManager<ApplicationUser> signIn, UserManager<ApplicationUser> users,
        IFileStorageService files, IAuditService audit)
    {
        _signIn = signIn;
        _users = users;
        _files = files;
        _audit = audit;
    }

    [AllowAnonymous]
    [HttpGet]
    public IActionResult Login(string? returnUrl = null)
    {
        if (User.Identity?.IsAuthenticated == true)
            return RedirectToAction("Index", "Dashboard");

        return View(new LoginViewModel { ReturnUrl = returnUrl });
    }

    [AllowAnonymous]
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model)
    {
        if (!ModelState.IsValid) return View(model);

        var user = await _users.FindByEmailAsync(model.Email)
                   ?? await _users.FindByNameAsync(model.Email);

        if (user is null)
        {
            ModelState.AddModelError(string.Empty, "بيانات الدخول غير صحيحة.");
            return View(model);
        }

        if (!user.IsActive)
        {
            ModelState.AddModelError(string.Empty, "هذا الحساب معطّل. يرجى مراجعة إدارة النظام.");
            return View(model);
        }

        var result = await _signIn.PasswordSignInAsync(user, model.Password, model.RememberMe, lockoutOnFailure: true);

        if (result.Succeeded)
        {
            user.LastLoginAt = DateTime.Now;
            await _users.UpdateAsync(user);
            await _audit.LogAsync("تسجيل دخول", nameof(ApplicationUser), user.Id);

            if (!string.IsNullOrEmpty(model.ReturnUrl) && Url.IsLocalUrl(model.ReturnUrl))
                return Redirect(model.ReturnUrl);

            return RedirectToAction("Index", "Dashboard");
        }

        if (result.IsLockedOut)
            ModelState.AddModelError(string.Empty, "تم قفل الحساب مؤقتاً بسبب المحاولات المتكررة. حاول بعد 10 دقائق.");
        else
            ModelState.AddModelError(string.Empty, "بيانات الدخول غير صحيحة.");

        return View(model);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await _audit.LogAsync("تسجيل خروج");
        await _signIn.SignOutAsync();
        return RedirectToAction(nameof(Login));
    }

    [AllowAnonymous]
    public IActionResult AccessDenied() => View();

    [HttpGet]
    public async Task<IActionResult> Profile()
    {
        var user = await _users.GetUserAsync(User);
        if (user is null) return Challenge();

        return View(new ProfileViewModel
        {
            Id = user.Id,
            FullName = user.FullName,
            Email = user.Email ?? "",
            PhoneNumber = user.PhoneNumber,
            PhotoPath = user.PhotoPath,
            Roles = (await _users.GetRolesAsync(user)).ToList(),
            CreatedAt = user.CreatedAt,
            LastLoginAt = user.LastLoginAt
        });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Profile(ProfileViewModel model)
    {
        var user = await _users.GetUserAsync(User);
        if (user is null) return Challenge();

        if (!ModelState.IsValid)
        {
            model.Roles = (await _users.GetRolesAsync(user)).ToList();
            model.PhotoPath = user.PhotoPath;
            return View(model);
        }

        user.FullName = model.FullName;
        user.PhoneNumber = model.PhoneNumber;

        if (model.Photo is not null)
        {
            try
            {
                var path = await _files.SaveAsync(model.Photo, "users", IFileStorageService.ImageExtensions, 3 * 1024 * 1024);
                if (path is not null)
                {
                    _files.Delete(user.PhotoPath);
                    user.PhotoPath = path;
                }
            }
            catch (InvalidOperationException ex)
            {
                ModelState.AddModelError(nameof(model.Photo), ex.Message);
                model.Roles = (await _users.GetRolesAsync(user)).ToList();
                model.PhotoPath = user.PhotoPath;
                return View(model);
            }
        }

        await _users.UpdateAsync(user);
        await _signIn.RefreshSignInAsync(user);

        Success("تم تحديث الملف الشخصي بنجاح.");
        return RedirectToAction(nameof(Profile));
    }

    [HttpGet]
    public IActionResult ChangePassword() => View(new ChangePasswordViewModel());

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangePassword(ChangePasswordViewModel model)
    {
        if (!ModelState.IsValid) return View(model);

        var user = await _users.GetUserAsync(User);
        if (user is null) return Challenge();

        var result = await _users.ChangePasswordAsync(user, model.CurrentPassword, model.NewPassword);
        if (!result.Succeeded)
        {
            foreach (var e in result.Errors)
                ModelState.AddModelError(string.Empty, TranslateIdentityError(e));
            return View(model);
        }

        await _signIn.RefreshSignInAsync(user);
        await _audit.LogAsync("تغيير كلمة المرور", nameof(ApplicationUser), user.Id);

        Success("تم تغيير كلمة المرور بنجاح.");
        return RedirectToAction(nameof(Profile));
    }

    internal static string TranslateIdentityError(IdentityError e) => e.Code switch
    {
        "PasswordMismatch" => "كلمة المرور الحالية غير صحيحة.",
        "PasswordTooShort" => "كلمة المرور قصيرة جداً.",
        "PasswordRequiresDigit" => "كلمة المرور يجب أن تحتوي على رقم.",
        "PasswordRequiresLower" => "كلمة المرور يجب أن تحتوي على حرف صغير.",
        "PasswordRequiresUpper" => "كلمة المرور يجب أن تحتوي على حرف كبير.",
        "PasswordRequiresNonAlphanumeric" => "كلمة المرور يجب أن تحتوي على رمز خاص.",
        "DuplicateUserName" => "اسم المستخدم مستخدم مسبقاً.",
        "DuplicateEmail" => "البريد الإلكتروني مستخدم مسبقاً.",
        "InvalidEmail" => "صيغة البريد الإلكتروني غير صحيحة.",
        _ => e.Description
    };
}
