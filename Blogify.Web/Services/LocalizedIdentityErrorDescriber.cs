using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Localization;

namespace Blogify.Web.Services;

public sealed class LocalizedIdentityErrorDescriber(IStringLocalizer<SharedResource> localizer) : IdentityErrorDescriber
{
    public override IdentityError DefaultError()
        => new() { Code = nameof(DefaultError), Description = localizer["Identity.DefaultError"].Value };

    public override IdentityError ConcurrencyFailure()
        => new() { Code = nameof(ConcurrencyFailure), Description = localizer["Identity.ConcurrencyFailure"].Value };

    public override IdentityError PasswordMismatch()
        => new() { Code = nameof(PasswordMismatch), Description = localizer["Identity.PasswordMismatch"].Value };

    public override IdentityError InvalidToken()
        => new() { Code = nameof(InvalidToken), Description = localizer["Identity.InvalidToken"].Value };

    public override IdentityError LoginAlreadyAssociated()
        => new() { Code = nameof(LoginAlreadyAssociated), Description = localizer["Identity.LoginAlreadyAssociated"].Value };

    public override IdentityError InvalidUserName(string? userName)
        => new() { Code = nameof(InvalidUserName), Description = localizer["Identity.InvalidUserName", userName!].Value };

    public override IdentityError InvalidEmail(string? email)
        => new() { Code = nameof(InvalidEmail), Description = localizer["Identity.InvalidEmail", email!].Value };

    public override IdentityError DuplicateUserName(string userName)
        => new() { Code = nameof(DuplicateUserName), Description = localizer["Identity.DuplicateUserName", userName].Value };

    public override IdentityError DuplicateEmail(string email)
        => new() { Code = nameof(DuplicateEmail), Description = localizer["Identity.DuplicateEmail", email].Value };

    public override IdentityError InvalidRoleName(string? role)
        => new() { Code = nameof(InvalidRoleName), Description = localizer["Identity.InvalidRoleName", role!].Value };

    public override IdentityError DuplicateRoleName(string role)
        => new() { Code = nameof(DuplicateRoleName), Description = localizer["Identity.DuplicateRoleName", role].Value };

    public override IdentityError UserAlreadyHasPassword()
        => new() { Code = nameof(UserAlreadyHasPassword), Description = localizer["Identity.UserAlreadyHasPassword"].Value };

    public override IdentityError UserLockoutNotEnabled()
        => new() { Code = nameof(UserLockoutNotEnabled), Description = localizer["Identity.UserLockoutNotEnabled"].Value };

    public override IdentityError UserAlreadyInRole(string role)
        => new() { Code = nameof(UserAlreadyInRole), Description = localizer["Identity.UserAlreadyInRole", role].Value };

    public override IdentityError UserNotInRole(string role)
        => new() { Code = nameof(UserNotInRole), Description = localizer["Identity.UserNotInRole", role].Value };

    public override IdentityError PasswordTooShort(int length)
        => new() { Code = nameof(PasswordTooShort), Description = localizer["Identity.PasswordTooShort", length].Value };

    public override IdentityError PasswordRequiresNonAlphanumeric()
        => new() { Code = nameof(PasswordRequiresNonAlphanumeric), Description = localizer["Identity.PasswordRequiresNonAlphanumeric"].Value };

    public override IdentityError PasswordRequiresDigit()
        => new() { Code = nameof(PasswordRequiresDigit), Description = localizer["Identity.PasswordRequiresDigit"].Value };

    public override IdentityError PasswordRequiresLower()
        => new() { Code = nameof(PasswordRequiresLower), Description = localizer["Identity.PasswordRequiresLower"].Value };

    public override IdentityError PasswordRequiresUpper()
        => new() { Code = nameof(PasswordRequiresUpper), Description = localizer["Identity.PasswordRequiresUpper"].Value };

    public override IdentityError PasswordRequiresUniqueChars(int uniqueChars)
        => new() { Code = nameof(PasswordRequiresUniqueChars), Description = localizer["Identity.PasswordRequiresUniqueChars", uniqueChars].Value };
}
