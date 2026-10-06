using System.Net;
using System.Net.Mail;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace AMIS.API;

public sealed record LoginRequest(string Email, string Password, bool RememberMe);
public sealed record EmailRequest(string Email);
public sealed record ResetRequest(string Email, string Otp, string Password);
public sealed record ActivationRequest(string Email, string Token, string Password);
public sealed record UserRequest(string FullName, string Email, string? Role);
public sealed record UserEditRequest(string FullName, string? Email, string? Role);
public sealed record ProfileRequest(string FullName);
public sealed record PasswordChangeConfirmRequest(string Otp, string NewPassword);

public sealed class EmailService(IConfiguration config) {
    public async Task SendAsync(string to, string subject, string body) {
        var host = config["EMAIL_HOST"] ?? throw new InvalidOperationException("EMAIL_HOST is required");
        using var message = new MailMessage(config["EMAIL_FROM"] ?? config["EMAIL_USERNAME"]!, to) { Subject = subject, Body = $"<html><body style='font-family:Arial,sans-serif;color:#102743;background:#f6f8fb;padding:28px'><div style='max-width:560px;margin:auto;background:white;border-radius:14px;padding:30px'><h1 style='color:#123a70'>Talaan</h1>{body}<p style='margin-top:30px;color:#65758b;font-size:13px'>Attendance Management Information System</p></div></body></html>", IsBodyHtml = true };
        using var client = new SmtpClient(host, int.Parse(config["EMAIL_PORT"] ?? "587")) { EnableSsl = bool.Parse(config["EMAIL_USE_SSL"] ?? "true") };
        if (!string.IsNullOrWhiteSpace(config["EMAIL_USERNAME"])) client.Credentials = new NetworkCredential(config["EMAIL_USERNAME"], config["EMAIL_PASSWORD"]);
        await client.SendMailAsync(message);
    }
}

public static class AuthEndpoints {
    private static readonly string[] Weak = ["password", "qwerty", "admin", "welcome", "123456"];
    public static bool InvitableStaffRole(string? role) => role is "Administrator" or "Teacher";
    private static async Task<bool> IsStaff(UserManager<AppUser> users, AppUser user) =>
        await users.IsInRoleAsync(user, "Administrator") || await users.IsInRoleAsync(user, "Teacher");
    public static bool Strong(string password) => password.Length >= 12 && password.Any(char.IsUpper) && password.Any(char.IsLower) && password.Any(char.IsDigit) && password.Any(c => !char.IsLetterOrDigit(c)) && !Weak.Any(w => password.Contains(w, StringComparison.OrdinalIgnoreCase));
    public static string HashToken(string value) { var salt = RandomNumberGenerator.GetBytes(16); var hash = Rfc2898DeriveBytes.Pbkdf2(value, salt, 100_000, HashAlgorithmName.SHA256, 32); return $"{Convert.ToHexString(salt)}:{Convert.ToHexString(hash)}"; }
    public static bool VerifyToken(string stored, string value) { var parts = stored.Split(':'); if (parts.Length != 2) return false; var salt = Convert.FromHexString(parts[0]); var expected = Convert.FromHexString(parts[1]); var actual = Rfc2898DeriveBytes.Pbkdf2(value, salt, 100_000, HashAlgorithmName.SHA256, 32); return CryptographicOperations.FixedTimeEquals(expected, actual); }
    public static void MapAuth(this WebApplication app) {
        var api = app.MapGroup("/api/auth");
        api.MapPost("/login", async (LoginRequest input, UserManager<AppUser> users, SignInManager<AppUser> signIn) => {
            var user = await users.FindByEmailAsync(input.Email.Trim());
            if (user is null || !user.IsActive || !user.EmailConfirmed || !await IsStaff(users, user))
                return Results.Json(new { message = "Invalid email or password." }, statusCode: 401);
            var result = await signIn.PasswordSignInAsync(user, input.Password, input.RememberMe, lockoutOnFailure: true);
            return result.Succeeded
                ? Results.Ok(new { user.Id, user.FullName, user.Email, roles = await users.GetRolesAsync(user) })
                : Results.Json(new { message = "Invalid email or password." }, statusCode: 401);
        });
        api.MapPost("/logout", async (SignInManager<AppUser> signIn) => { await signIn.SignOutAsync(); return Results.NoContent(); }).RequireAuthorization();
        api.MapGet("/me", async (HttpContext ctx, UserManager<AppUser> users) => { var user = await users.GetUserAsync(ctx.User); return user is null ? Results.Unauthorized() : Results.Ok(new { user.Id, user.FullName, user.Email, roles = await users.GetRolesAsync(user) }); }).RequireAuthorization("Staff");
        api.MapPut("/profile", async (ProfileRequest input, HttpContext ctx, UserManager<AppUser> users) => {
            var user = await users.GetUserAsync(ctx.User);
            if (user is null) return Results.Unauthorized();
            var name = input.FullName.Trim();
            if (name.Length < 2 || name.Length > 100) return Results.BadRequest(new { message = "Full name must be 2 to 100 characters." });
            user.FullName = name;
            var result = await users.UpdateAsync(user);
            return result.Succeeded ? Results.Ok(new { user.Id, user.FullName, user.Email, roles = await users.GetRolesAsync(user) }) : Results.BadRequest(new { message = string.Join(" ", result.Errors.Select(e => e.Description)) });
        }).RequireAuthorization("Staff");
        api.MapPost("/change-password/request", async (HttpContext ctx,
            UserManager<AppUser> users, AppDb db, EmailService email, ILogger<EmailService> logger) => {
            var user = await users.GetUserAsync(ctx.User);
            if (user is null) return Results.Unauthorized();
            var recent = await db.OneTimeTokens.AnyAsync(t => t.UserId == user.Id && t.Purpose == "change-password" &&
                !t.Used && t.ExpiresAt > DateTimeOffset.UtcNow.AddMinutes(9));
            if (recent) return Results.Json(new { message = "Wait one minute before requesting another code." }, statusCode: 429);
            var outstanding = await db.OneTimeTokens.Where(t => t.UserId == user.Id && t.Purpose == "change-password" && !t.Used).ToListAsync();
            foreach (var old in outstanding) old.Used = true;
            var otp = RandomNumberGenerator.GetInt32(0, 1000000).ToString("D6");
            var token = new OneTimeToken { UserId = user.Id, Purpose = "change-password", Hash = HashToken(otp),
                ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(10) };
            db.OneTimeTokens.Add(token);
            await db.SaveChangesAsync();
            try { await email.SendAsync(user.Email!, "Talaan password change code",
                $"<p>Use this six-digit code to change your Talaan password. It expires in 10 minutes.</p><h2>{otp}</h2><p>If you did not request this, contact your school administrator.</p>"); }
            catch (Exception ex) {
                logger.LogError(ex, "Password change code delivery failed");
                token.Used = true;
                await db.SaveChangesAsync();
                return Results.Problem("Could not send the code to your registered email.", statusCode: 503);
            }
            return Results.Ok(new { message = "A six-digit code was sent to your registered email." });
        }).RequireAuthorization("Staff");
        api.MapPost("/change-password/confirm", async (PasswordChangeConfirmRequest input, HttpContext ctx,
            UserManager<AppUser> users, AppDb db, SignInManager<AppUser> signIn) => {
            var user = await users.GetUserAsync(ctx.User);
            if (user is null) return Results.Unauthorized();
            if (!Strong(input.NewPassword)) return Results.BadRequest(new { message = "New password does not meet strength requirements." });
            var token = await db.OneTimeTokens.Where(t => t.UserId == user.Id && t.Purpose == "change-password" && !t.Used)
                .OrderByDescending(t => t.ExpiresAt).FirstOrDefaultAsync();
            if (token is null || token.ExpiresAt <= DateTimeOffset.UtcNow || token.Attempts >= 5)
                return Results.BadRequest(new { message = "Invalid or expired code." });
            token.Attempts++;
            if (input.Otp is null || input.Otp.Length != 6 || !input.Otp.All(char.IsDigit) || !VerifyToken(token.Hash, input.Otp)) {
                await db.SaveChangesAsync();
                return Results.BadRequest(new { message = "Invalid or expired code." });
            }
            var resetToken = await users.GeneratePasswordResetTokenAsync(user);
            var result = await users.ResetPasswordAsync(user, resetToken, input.NewPassword);
            if (!result.Succeeded) return Results.BadRequest(new { message = "Unable to change password." });
            var pending = await db.OneTimeTokens.Where(t => t.UserId == user.Id && t.Purpose == "change-password" && !t.Used).ToListAsync();
            foreach (var item in pending) item.Used = true;
            await db.SaveChangesAsync();
            await signIn.RefreshSignInAsync(user);
            return Results.Ok(new { message = "Password updated." });
        }).RequireAuthorization("Staff");
        api.MapPost("/test-notification", async (HttpContext ctx, UserManager<AppUser> users, EmailService email, ILogger<EmailService> logger) => {
            var user = await users.GetUserAsync(ctx.User);
            if (user?.Email is null) return Results.Unauthorized();
            try { await email.SendAsync(user.Email, "Talaan notification test", "<p>This test confirms that Talaan can submit attendance notifications through the configured email service.</p><p>No student attendance was changed.</p>"); }
            catch (Exception ex) { logger.LogError(ex, "Test notification delivery failed"); return Results.Problem("The email service could not send the test notification.", statusCode: 503); }
            return Results.Ok(new { message = "Test notification accepted by the email server." });
        }).RequireAuthorization("Administrator");
        api.MapPost("/forgot", async (EmailRequest input, UserManager<AppUser> users, AppDb db, EmailService email, ILogger<EmailService> logger) => {
            var user = await users.FindByEmailAsync(input.Email.Trim());
            if (user is not null && user.IsActive && user.EmailConfirmed && await IsStaff(users, user)) {
                var recent = await db.OneTimeTokens.AnyAsync(x => x.UserId == user.Id && x.Purpose == "reset" && x.ExpiresAt > DateTimeOffset.UtcNow.AddMinutes(8) && !x.Used);
                if (!recent) {
                    var otp = RandomNumberGenerator.GetInt32(0, 1000000).ToString("D6");
                    db.OneTimeTokens.Add(new OneTimeToken { UserId = user.Id, Purpose = "reset", Hash = HashToken(otp), ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(10) });
                    await db.SaveChangesAsync();
                    try { await email.SendAsync(user.Email!, "Password reset code", $"<p>Use this six-digit code to reset your password. It expires in 10 minutes.</p><h2>{otp}</h2><p>If you did not request this, ignore this message.</p>"); }
                    catch (Exception ex) { logger.LogError(ex, "Password reset email delivery failed"); var pending = await db.OneTimeTokens.Where(x => x.UserId == user.Id && x.Purpose == "reset" && !x.Used).OrderByDescending(x => x.ExpiresAt).FirstAsync(); pending.Used = true; await db.SaveChangesAsync(); }
                }
            }
            return Results.Ok(new { message = "If the account exists, a reset code has been sent." });
        });
        api.MapPost("/reset", async (ResetRequest input, UserManager<AppUser> users, AppDb db) => {
            if (!Strong(input.Password)) return Results.BadRequest(new { message = "Password does not meet strength requirements." });
            var user = await users.FindByEmailAsync(input.Email.Trim());
            if (user is null || !user.IsActive || !await IsStaff(users, user)) return Results.BadRequest(new { message = "Invalid or expired code." });
            var token = await db.OneTimeTokens.Where(x => x.UserId == user.Id && x.Purpose == "reset" && !x.Used).OrderByDescending(x => x.ExpiresAt).FirstOrDefaultAsync();
            if (token is null || token.ExpiresAt < DateTimeOffset.UtcNow || token.Attempts >= 5) return Results.BadRequest(new { message = "Invalid or expired code." });
            token.Attempts++;
            if (input.Otp.Length != 6 || !input.Otp.All(char.IsDigit) || !VerifyToken(token.Hash, input.Otp)) { await db.SaveChangesAsync(); return Results.BadRequest(new { message = "Invalid or expired code." }); }
            var result = await users.ResetPasswordAsync(user, await users.GeneratePasswordResetTokenAsync(user), input.Password);
            if (!result.Succeeded) return Results.BadRequest(new { message = string.Join(" ", result.Errors.Select(e => e.Description)) });
            token.Used = true; await db.SaveChangesAsync(); await users.UpdateSecurityStampAsync(user);
            return Results.Ok(new { message = "Password updated." });
        });
        api.MapPost("/activate", async (ActivationRequest input, UserManager<AppUser> users, AppDb db, SignInManager<AppUser> signIn) => {
            if (!Strong(input.Password)) return Results.BadRequest(new { message = "Password does not meet strength requirements." });
            var user = await users.FindByEmailAsync(input.Email.Trim());
            if (user is null || !user.IsActive || !await IsStaff(users, user)) return Results.BadRequest(new { message = "Invalid activation link." });
            var token = await db.OneTimeTokens.Where(x => x.UserId == user.Id && x.Purpose == "activation" && !x.Used).OrderByDescending(x => x.ExpiresAt).FirstOrDefaultAsync();
            if (token is null || token.ExpiresAt < DateTimeOffset.UtcNow || !VerifyToken(token.Hash, input.Token)) return Results.BadRequest(new { message = "Invalid activation link." });
            var result = await users.AddPasswordAsync(user, input.Password);
            if (!result.Succeeded) return Results.BadRequest(new { message = string.Join(" ", result.Errors.Select(e => e.Description)) });
            user.EmailConfirmed = true; token.Used = true; await users.UpdateAsync(user); await db.SaveChangesAsync(); await signIn.SignInAsync(user, false);
            return Results.Ok(new { message = "Account activated." });
        });
        api.MapPost("/users", async (UserRequest input, UserManager<AppUser> users, AppDb db, EmailService email, IConfiguration config) => {
            var role = input.Role ?? "Teacher";
            if (!InvitableStaffRole(role)) return Results.BadRequest(new { message = "Choose Administrator or Teacher." });
            if (string.IsNullOrWhiteSpace(input.FullName) || input.FullName.Trim().Length > 100)
                return Results.BadRequest(new { message = "Enter a full name up to 100 characters." });
            if (await users.FindByEmailAsync(input.Email.Trim()) is not null) return Results.Conflict(new { message = "Email already exists." });
            var user = new AppUser { FullName = input.FullName.Trim(), UserName = input.Email.Trim(), Email = input.Email.Trim() };
            var result = await users.CreateAsync(user); if (!result.Succeeded) return Results.BadRequest(new { message = string.Join(" ", result.Errors.Select(e => e.Description)) });
            await users.AddToRoleAsync(user, role);
            var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
            db.OneTimeTokens.Add(new OneTimeToken { UserId = user.Id, Purpose = "activation", Hash = HashToken(token), ExpiresAt = DateTimeOffset.UtcNow.AddDays(2) }); await db.SaveChangesAsync();
            var frontendUrl = string.IsNullOrWhiteSpace(config["PUBLIC_FRONTEND_URL"])
                ? config["FRONTEND_URL"] ?? "http://localhost:3000"
                : config["PUBLIC_FRONTEND_URL"]!;
            var link = $"{frontendUrl.TrimEnd('/')}/activate?email={Uri.EscapeDataString(user.Email!)}&token={token}";
            await email.SendAsync(user.Email!, "Activate your Talaan account", $"<p>Welcome, {WebUtility.HtmlEncode(user.FullName)}. Set your password to activate your account.</p><p><a href='{link}' style='background:#2365ae;color:white;padding:12px 18px;border-radius:8px;text-decoration:none'>Activate account</a></p><p>This link expires in 48 hours.</p>");
            return Results.Created($"/api/users/{user.Id}", new { user.Id, user.FullName, user.Email, role });
        }).RequireAuthorization("Administrator");
        api.MapPut("/users/{id:guid}", async (Guid id, UserEditRequest input, HttpContext ctx,
            UserManager<AppUser> users, AppDb db) => {
            var target = await users.FindByIdAsync(id.ToString());
            if (target is null) return Results.NotFound();
            var name = input.FullName?.Trim() ?? "";
            if (name.Length < 2 || name.Length > 100)
                return Results.BadRequest(new { message = "Full name must be 2 to 100 characters." });
            var currentRole = (await users.GetRolesAsync(target)).FirstOrDefault();
            var role = input.Role ?? currentRole;
            if (currentRole == "Guardian" && role != "Guardian" || currentRole != "Guardian" && !InvitableStaffRole(role))
                return Results.BadRequest(new { message = "Guardian contacts cannot become staff; choose Administrator or Teacher for staff." });
            var email = input.Email?.Trim() ?? target.Email ?? "";
            if (!MailAddress.TryCreate(email, out var address) || !string.Equals(address.Address, email, StringComparison.OrdinalIgnoreCase))
                return Results.BadRequest(new { message = "Enter a valid email address." });
            var existing = await users.FindByEmailAsync(email);
            if (existing is not null && existing.Id != id) return Results.Conflict(new { message = "Email already exists." });
            var isSelf = users.GetUserId(ctx.User) == id.ToString();
            if (isSelf && (!string.Equals(email, target.Email, StringComparison.OrdinalIgnoreCase) || role != currentRole))
                return Results.BadRequest(new { message = "Use Settings to manage your own account; your role cannot be changed here." });
            var emailChanged = !string.Equals(email, target.Email, StringComparison.OrdinalIgnoreCase);
            await using var transaction = await db.Database.BeginTransactionAsync();
            target.FullName = name;
            target.Email = email;
            target.UserName = email;
            var updated = await users.UpdateAsync(target);
            if (!updated.Succeeded) return Results.BadRequest(new { message = string.Join(" ", updated.Errors.Select(e => e.Description)) });
            if (role != currentRole) {
                if (currentRole is not null) {
                    var removed = await users.RemoveFromRoleAsync(target, currentRole);
                    if (!removed.Succeeded) return Results.BadRequest(new { message = string.Join(" ", removed.Errors.Select(e => e.Description)) });
                }
                var added = await users.AddToRoleAsync(target, role!);
                if (!added.Succeeded) return Results.BadRequest(new { message = string.Join(" ", added.Errors.Select(e => e.Description)) });
            }
            if (emailChanged) await users.UpdateSecurityStampAsync(target);
            await transaction.CommitAsync();
            return Results.Ok(new { target.Id, target.FullName, target.Email, role });
        }).RequireAuthorization("Administrator");
        api.MapDelete("/users/{id:guid}", async (Guid id, HttpContext ctx, UserManager<AppUser> users, AppDb db) => {
            var target = await users.FindByIdAsync(id.ToString());
            if (target is null) return Results.NotFound();
            if (users.GetUserId(ctx.User) == id.ToString())
                return Results.BadRequest(new { message = "You cannot delete your own account." });
            var linked = await db.GuardianStudents.AnyAsync(x => x.GuardianId == id)
                || await db.Sections.AnyAsync(x => x.AdviserId == id)
                || await db.Schedules.AnyAsync(x => x.TeacherId == id)
                || await db.Attendance.AnyAsync(x => x.RecordedById == id)
                || await db.AttendanceSubmissions.AnyAsync(x => x.SubmittedById == id);
            if (linked) return Results.Conflict(new { message = "This user is linked to students, classes, or attendance history and cannot be deleted." });
            await using var transaction = await db.Database.BeginTransactionAsync();
            await db.OneTimeTokens.Where(x => x.UserId == id).ExecuteDeleteAsync();
            var deleted = await users.DeleteAsync(target);
            if (!deleted.Succeeded) return Results.BadRequest(new { message = string.Join(" ", deleted.Errors.Select(e => e.Description)) });
            await transaction.CommitAsync();
            return Results.NoContent();
        }).RequireAuthorization("Administrator");
    }
}
