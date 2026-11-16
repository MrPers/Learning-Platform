using ELearningPlatform.Application.Validators;
using ELearningPlatform.Application.Validators.Authentication;
using FluentValidation.TestHelper;
using Microsoft.Extensions.Options;

namespace ELearningPlatform.Application.Tests.Validators;

public class AuthValidatorTests
{
    private const string Good = "Str0ng!Pass";
    private static readonly IOptions<UploadsSettings> Uploads = Options.Create(new UploadsSettings
    {
        ImageSizeInMB = 1, VideoSizeInMB = 2, ImageExtentionAllowed = [".png", ".jpg"], VideoExtentionAllowed = [".mp4"],
    });

    private static RegisterStudentDto Register() => new()
    {
        FirstName = "A", LastName = "B", PhoneNumber = "+201012345678", Email = "a@b.com", Gender = Gender.Male,
        Password = Good, ConfirmPassword = Good, Level = Level.Beginner,
    };

    // ---- password policy ----

    [Theory]
    [InlineData("Short1!")]
    [InlineData("alllowercase1!")]
    [InlineData("ALLUPPERCASE1!")]
    [InlineData("NoDigits!!")]
    [InlineData("NoSymbols123")]
    [InlineData("")]
    public void Weak_passwords_are_rejected_on_register(string password)
    {
        var dto = Register();
        dto.Password = dto.ConfirmPassword = password;

        new RegisterStudentDtoValidator(Uploads).TestValidate(dto).ShouldHaveValidationErrorFor(x => x.Password);
    }

    [Theory]
    [InlineData("Str0ng!Pass")]
    [InlineData("Aa1!aaaa")]
    [InlineData("P@ssw0rd-with spaces")]
    public void Strong_passwords_are_accepted(string password)
    {
        var dto = Register();
        dto.Password = dto.ConfirmPassword = password;

        new RegisterStudentDtoValidator(Uploads).TestValidate(dto).ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Password_longer_than_max_is_rejected()
    {
        var dto = Register();
        dto.Password = dto.ConfirmPassword = "Aa1!" + new string('x', PasswordRules.MaxLength);

        new RegisterStudentDtoValidator(Uploads).TestValidate(dto).ShouldHaveValidationErrorFor(x => x.Password);
    }

    [Fact]
    public void Confirm_password_must_match()
    {
        var dto = Register();
        dto.ConfirmPassword = "Other!Pass1";

        new RegisterStudentDtoValidator(Uploads).TestValidate(dto).ShouldHaveValidationErrorFor(x => x.ConfirmPassword);
    }

    // ---- registration fields ----

    [Theory]
    [InlineData("+201012345678", true)]
    [InlineData("+201112345678", true)]
    [InlineData("+201512345678", true)]
    [InlineData("+201312345678", false)]
    [InlineData("01012345678", false)]
    [InlineData("+2010123456", false)]
    [InlineData("", false)]
    public void Phone_number_format(string phone, bool valid)
    {
        var dto = Register();
        dto.PhoneNumber = phone;

        var result = new RegisterStudentDtoValidator(Uploads).TestValidate(dto);

        if (valid) result.ShouldNotHaveValidationErrorFor(x => x.PhoneNumber);
        else result.ShouldHaveValidationErrorFor(x => x.PhoneNumber);
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-an-email")]
    public void Register_email_must_be_valid(string email)
    {
        var dto = Register();
        dto.Email = email;

        new RegisterStudentDtoValidator(Uploads).TestValidate(dto).ShouldHaveValidationErrorFor(x => x.Email);
    }

    [Fact]
    public void Register_names_are_required_and_limited()
    {
        var dto = Register();
        dto.FirstName = "";
        dto.LastName = new string('x', 51);

        var result = new RegisterStudentDtoValidator(Uploads).TestValidate(dto);

        result.ShouldHaveValidationErrorFor(x => x.FirstName);
        result.ShouldHaveValidationErrorFor(x => x.LastName);
    }

    [Fact]
    public void Register_enums_must_be_defined()
    {
        var dto = Register();
        dto.Gender = (Gender)99;
        dto.Level = (Level)99;

        var result = new RegisterStudentDtoValidator(Uploads).TestValidate(dto);

        result.ShouldHaveValidationErrorFor(x => x.Gender);
        result.ShouldHaveValidationErrorFor(x => x.Level);
    }

    [Fact]
    public void Teacher_specialization_must_be_defined()
    {
        var dto = new RegisterTeacherDto
        {
            FirstName = "A", LastName = "B", PhoneNumber = "+201012345678", Email = "a@b.com", Gender = Gender.Male,
            Password = Good, ConfirmPassword = Good, Specialization = (Specialization)0,
        };

        new RegisterTeacherDtoValidator(Uploads).TestValidate(dto).ShouldHaveValidationErrorFor(x => x.Specialization);
    }

    [Fact]
    public void Profile_picture_is_optional()
    {
        new RegisterStudentDtoValidator(Uploads).TestValidate(Register()).ShouldNotHaveValidationErrorFor(x => x.ProfilePictureFile);
    }

    [Fact]
    public void Valid_profile_picture_is_accepted()
    {
        var dto = Register();
        dto.ProfilePictureFile = FormFiles.Create("me.png", FormFiles.Png);

        new RegisterStudentDtoValidator(Uploads).TestValidate(dto).ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData("me.gif")]
    [InlineData("me.exe")]
    public void Profile_picture_with_wrong_extension_is_rejected(string name)
    {
        var dto = Register();
        dto.ProfilePictureFile = FormFiles.Create(name, FormFiles.Png);

        new RegisterStudentDtoValidator(Uploads).TestValidate(dto).ShouldHaveValidationErrorFor(x => x.ProfilePictureFile);
    }

    [Fact]
    public void Executable_renamed_to_png_is_rejected()
    {
        var dto = Register();
        dto.ProfilePictureFile = FormFiles.Create("me.png", FormFiles.Exe);

        new RegisterStudentDtoValidator(Uploads).TestValidate(dto)
            .ShouldHaveValidationErrorFor(x => x.ProfilePictureFile)
            .WithErrorMessage("Profile picture content does not match its file type.");
    }

    [Fact]
    public void Oversized_profile_picture_is_rejected()
    {
        var dto = Register();
        dto.ProfilePictureFile = FormFiles.OfSize("me.png", FormFiles.Png, 1024 * 1024 + 1);

        new RegisterStudentDtoValidator(Uploads).TestValidate(dto).ShouldHaveValidationErrorFor(x => x.ProfilePictureFile);
    }

    // ---- login ----

    [Fact]
    public void Login_does_not_apply_the_password_policy()
    {
        new LoginDtoValidator().TestValidate(new LoginDto { Email = "a@b.com", Password = "old", Role = Role.Student })
            .ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData("", "p", Role.Student, "Email")]
    [InlineData("bad", "p", Role.Student, "Email")]
    [InlineData("a@b.com", "", Role.Student, "Password")]
    [InlineData("a@b.com", "p", (Role)42, "Role")]
    public void Login_invalid_inputs(string email, string password, Role role, string property)
    {
        new LoginDtoValidator().TestValidate(new LoginDto { Email = email, Password = password, Role = role })
            .ShouldHaveValidationErrorFor(property);
    }

    // ---- password change / reset ----

    [Fact]
    public void Change_password_requires_a_strong_and_different_password()
    {
        var validator = new ChangePasswordDtoValidator();

        validator.TestValidate(new ChangePasswordDto { CurrentPassword = "x", NewPassword = Good }).ShouldNotHaveAnyValidationErrors();
        validator.TestValidate(new ChangePasswordDto { CurrentPassword = "x", NewPassword = "weak" }).ShouldHaveValidationErrorFor(x => x.NewPassword);
        validator.TestValidate(new ChangePasswordDto { CurrentPassword = Good, NewPassword = Good }).ShouldHaveValidationErrorFor(x => x.NewPassword);
        validator.TestValidate(new ChangePasswordDto { CurrentPassword = "", NewPassword = Good }).ShouldHaveValidationErrorFor(x => x.CurrentPassword);
    }

    [Fact]
    public void Reset_password_rules()
    {
        var validator = new ResetPasswordDtoValidator();

        validator.TestValidate(new ResetPasswordDto { Email = "a@b.com", Token = "t", NewPassword = Good }).ShouldNotHaveAnyValidationErrors();
        var bad = validator.TestValidate(new ResetPasswordDto { Email = "x", Token = "", NewPassword = "weak" });
        bad.ShouldHaveValidationErrorFor(x => x.Email);
        bad.ShouldHaveValidationErrorFor(x => x.Token);
        bad.ShouldHaveValidationErrorFor(x => x.NewPassword);
    }

    // ---- email-only DTOs ----

    [Theory]
    [InlineData("a@b.com", true)]
    [InlineData("", false)]
    [InlineData("nope", false)]
    public void Email_only_dtos(string email, bool valid)
    {
        var results = new[]
        {
            new ForgotPasswordDtoValidator().TestValidate(new ForgotPasswordDto { Email = email }).IsValid,
            new ResendConfirmEmailDtoValidator().TestValidate(new ResendConfirmEmailDto { Email = email }).IsValid,
            new ConfirmEmailDtoValidator().TestValidate(new ConfirmEmailDto { Email = email, Token = "t" }).IsValid,
        };

        Assert.All(results, r => Assert.Equal(valid, r));
    }

    [Fact]
    public void Email_longer_than_200_is_rejected()
    {
        var email = new string('a', 195) + "@b.com";

        new ForgotPasswordDtoValidator().TestValidate(new ForgotPasswordDto { Email = email }).ShouldHaveValidationErrorFor(x => x.Email);
    }

    [Fact]
    public void Confirm_email_requires_token()
    {
        new ConfirmEmailDtoValidator().TestValidate(new ConfirmEmailDto { Email = "a@b.com", Token = "" }).ShouldHaveValidationErrorFor(x => x.Token);
    }

    // ---- two factor ----

    [Theory]
    [InlineData("123456", true)]
    [InlineData("12345", false)]
    [InlineData("1234567", false)]
    [InlineData("12a456", false)]
    [InlineData("", false)]
    public void Two_factor_code_must_be_six_digits(string code, bool valid)
    {
        var result = new VerifyTwoFactorDtoValidator().TestValidate(new VerifyTwoFactorDto { Email = "a@b.com", Code = code, Method = TwoFactorMethod.Email });

        Assert.Equal(valid, result.IsValid);
    }

    [Theory]
    [InlineData(TwoFactorMethod.Email, true)]
    [InlineData(TwoFactorMethod.SMS, true)]
    [InlineData((TwoFactorMethod)0, false)]
    [InlineData((TwoFactorMethod)9, false)]
    public void Two_factor_method_must_be_known(TwoFactorMethod method, bool valid)
    {
        Assert.Equal(valid, new TwoFactorSendCodeDtoValidator().TestValidate(new TwoFactorSendCodeDto { Email = "a@b.com", Method = method }).IsValid);
        Assert.Equal(valid, new EnableTwoFactorDtoValidator().TestValidate(new EnableTwoFactorDto { Method = method }).IsValid);
    }

    [Fact]
    public void External_login_requires_token_and_provider()
    {
        var result = new ExternalLoginDtoValidator().TestValidate(new ExternalLoginDto());

        result.ShouldHaveValidationErrorFor(x => x.IdToken);
        result.ShouldHaveValidationErrorFor(x => x.Provider);
        new ExternalLoginDtoValidator().TestValidate(new ExternalLoginDto { IdToken = "t", Provider = "Google" }).ShouldNotHaveAnyValidationErrors();
    }
}
