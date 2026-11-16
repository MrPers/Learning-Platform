using ELearningPlatform.Application.Validators.Course;
using ELearningPlatform.Application.Validators.Exam;
using ELearningPlatform.Application.Validators.Notification;
using ELearningPlatform.Application.Validators.Question;
using ELearningPlatform.Application.Validators.React;
using ELearningPlatform.Application.Validators.User;
using ELearningPlatform.Application.Validators.Video;
using FluentValidation.TestHelper;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;

namespace ELearningPlatform.Application.Tests.Validators;

public class ContentValidatorTests
{
    private static readonly IOptions<UploadsSettings> Uploads = Options.Create(new UploadsSettings
    {
        ImageSizeInMB = 1, VideoSizeInMB = 1, ImageExtentionAllowed = [".png"], VideoExtentionAllowed = [".mp4", ".webm"],
    });
    private static readonly FakeTimeProvider Clock = new(new DateTimeOffset(2026, 10, 1, 12, 0, 0, TimeSpan.Zero));

    // ---- courses ----

    [Theory]
    [InlineData("Algebra", 10, 0, true)]
    [InlineData("A", 10, 0, false)]
    [InlineData("", 10, 0, false)]
    [InlineData("Algebra", 0, 0, false)]
    [InlineData("Algebra", 10001, 0, false)]
    [InlineData("Algebra", 10, -1, false)]
    [InlineData("Algebra", 10, 1000001, false)]
    [InlineData("Algebra", 10, 1000000, true)]
    public void Create_course(string name, int hours, double price, bool valid)
    {
        var result = new CreateCourseDtoValidator().TestValidate(new CreateCourseDto { Name = name, Hours = hours, Price = (decimal)price });

        Assert.Equal(valid, result.IsValid);
    }

    [Fact]
    public void Create_course_description_is_optional_but_limited()
    {
        var validator = new CreateCourseDtoValidator();

        validator.TestValidate(new CreateCourseDto { Name = "Ok", Hours = 1, Description = null }).ShouldNotHaveAnyValidationErrors();
        validator.TestValidate(new CreateCourseDto { Name = "Ok", Hours = 1, Description = new string('d', 4001) }).ShouldHaveValidationErrorFor(x => x.Description);
    }

    [Theory]
    [InlineData(null, null, null, true)]
    [InlineData("Ok", "Desc", 5, true)]
    [InlineData("A", null, null, false)]
    [InlineData(null, null, 0, false)]
    [InlineData(null, null, 10001, false)]
    public void Update_course(string? name, string? description, int? hours, bool valid)
    {
        Assert.Equal(valid, new UpdateCourseDtoValidator().TestValidate(new UpdateCourseDto { Name = name, Description = description, Hours = hours }).IsValid);
    }

    [Theory]
    [InlineData(0, true)]
    [InlineData(99.99, true)]
    [InlineData(-0.01, false)]
    [InlineData(2000000, false)]
    public void Update_price(double price, bool valid)
    {
        Assert.Equal(valid, new UpdateCoursePriceDtoValidator().TestValidate(new UpdateCoursePriceDto { NewPrice = (decimal)price }).IsValid);
    }

    // ---- exams ----

    [Theory]
    [InlineData(0, 60, true)]
    [InlineData(60, 60, true)]
    [InlineData(-1, 60, false)]
    [InlineData(10, 0, false)]
    [InlineData(10, 10080, true)]
    [InlineData(10, 10081, false)]
    public void Create_exam_start_must_not_be_in_the_past(int startOffsetMinutes, int duration, bool valid)
    {
        var dto = new CreateExamDto { Title = "T", DurationInMinutes = duration, StartAt = Clock.GetUtcNow().UtcDateTime.AddMinutes(startOffsetMinutes) };

        Assert.Equal(valid, new CreateExamDtoValidator(Clock).TestValidate(dto).IsValid);
    }

    [Fact]
    public void Create_exam_requires_title()
    {
        var dto = new CreateExamDto { Title = "", DurationInMinutes = 10, StartAt = Clock.GetUtcNow().UtcDateTime.AddDays(1) };

        new CreateExamDtoValidator(Clock).TestValidate(dto).ShouldHaveValidationErrorFor(x => x.Title);
    }

    [Theory]
    [InlineData(null, null, true)]
    [InlineData(30, 5, true)]
    [InlineData(0, null, false)]
    [InlineData(null, -5, false)]
    public void Update_exam_time(int? duration, int? startOffsetMinutes, bool valid)
    {
        var dto = new UpdateExamTimeDto
        {
            DurationInMinutes = duration,
            StartAt = startOffsetMinutes is { } m ? Clock.GetUtcNow().UtcDateTime.AddMinutes(m) : null,
        };

        Assert.Equal(valid, new UpdateExamTimeDtoValidator(Clock).TestValidate(dto).IsValid);
    }

    [Fact]
    public void Update_exam_limits()
    {
        var validator = new UpdateExamDtoValidator();

        validator.TestValidate(new UpdateExamDto()).ShouldNotHaveAnyValidationErrors();
        validator.TestValidate(new UpdateExamDto { Title = new string('t', 1001) }).ShouldHaveValidationErrorFor(x => x.Title);
        validator.TestValidate(new UpdateExamDto { Notes = new string('n', 4001) }).ShouldHaveValidationErrorFor(x => x.Notes);
    }

    [Theory]
    [InlineData(1, 1, true)]
    [InlineData(100, 4, true)]
    [InlineData(0, 1, false)]
    [InlineData(101, 1, false)]
    [InlineData(1, 0, false)]
    [InlineData(1, 5, false)]
    public void Submit_answer(int question, int option, bool valid)
    {
        Assert.Equal(valid, new SubmitExamDtoValidator().TestValidate(new SubmitExamDto { QuestionNumber = (byte)question, OptionNumber = (byte)option }).IsValid);
    }

    // ---- questions ----

    [Theory]
    [InlineData(1, true)]
    [InlineData(4, true)]
    [InlineData(0, false)]
    [InlineData(5, false)]
    public void Mcq_correct_index(int index, bool valid)
    {
        var dto = new CreateMcqQuestionDto { Header = "H", Mark = 1, Option1 = "a", Option2 = "b", Option3 = "c", Option4 = "d", CorrectAnswerIndex = index };

        Assert.Equal(valid, new CreateMcqQuestionDtoValidator().TestValidate(dto).IsValid);
    }

    [Fact]
    public void Mcq_requires_all_options_and_header()
    {
        var result = new CreateMcqQuestionDtoValidator().TestValidate(new CreateMcqQuestionDto { Mark = 1, CorrectAnswerIndex = 1 });

        result.ShouldHaveValidationErrorFor(x => x.Header);
        result.ShouldHaveValidationErrorFor(x => x.Option1);
        result.ShouldHaveValidationErrorFor(x => x.Option2);
        result.ShouldHaveValidationErrorFor(x => x.Option3);
        result.ShouldHaveValidationErrorFor(x => x.Option4);
    }

    [Theory]
    [InlineData(0.5, true)]
    [InlineData(10000, true)]
    [InlineData(0, false)]
    [InlineData(-1, false)]
    [InlineData(10000.5, false)]
    public void Question_marks(double mark, bool valid)
    {
        Assert.Equal(valid, new CreateTFQuestionDtoValidator().TestValidate(new CreateTFQuestionDto { Header = "H", Mark = mark }).IsValid);
        Assert.Equal(valid, new UpdateQuestionDegreeDtoValidator().TestValidate(new UpdateQuestionDegreeDto { Mark = mark }).IsValid);
    }

    // ---- reactions & comments ----

    [Theory]
    [InlineData("nice", true)]
    [InlineData("", false)]
    public void Comment(string text, bool valid)
    {
        Assert.Equal(valid, new AddCommentDtoValidator().TestValidate(new AddCommentDto { Comment = text }).IsValid);
    }

    [Fact]
    public void Comment_max_length()
    {
        new AddCommentDtoValidator().TestValidate(new AddCommentDto { Comment = new string('c', 1001) }).ShouldHaveValidationErrorFor(x => x.Comment);
    }

    [Theory]
    [InlineData(Rating.OneStar, true)]
    [InlineData(Rating.FiveStars, true)]
    [InlineData((Rating)0, false)]
    [InlineData((Rating)6, false)]
    public void Rating_must_be_one_to_five(Rating rating, bool valid)
    {
        Assert.Equal(valid, new CourseReactionDtoValidator().TestValidate(new CourseReactionDto { Rating = rating }).IsValid);
    }

    // ---- users ----

    [Theory]
    [InlineData("Ann", "Lee", true)]
    [InlineData("A", "Lee", false)]
    [InlineData("Ann", "", false)]
    public void Update_profile(string first, string last, bool valid)
    {
        Assert.Equal(valid, new UpdateProfileDtoValidator().TestValidate(new UpdateProfileDto { FirstName = first, LastName = last }).IsValid);
    }

    [Theory]
    [InlineData("0f8fad5b-d9cb-469f-a165-70867728950e", true)]
    [InlineData("short", false)]
    [InlineData("", false)]
    public void User_ids_must_be_guids_length(string id, bool valid)
    {
        Assert.Equal(valid, new UserActionDtoValidator().TestValidate(new UserActionDto { UserId = id }).IsValid);
        Assert.Equal(valid, new UserRoleDtoValidator().TestValidate(new UserRoleDto { UserId = id, Role = Role.Teacher }).IsValid);
    }

    [Fact]
    public void User_role_must_be_defined()
    {
        new UserRoleDtoValidator().TestValidate(new UserRoleDto { UserId = Guid.NewGuid().ToString(), Role = (Role)77 }).ShouldHaveValidationErrorFor(x => x.Role);
    }

    [Fact]
    public void Profile_picture_is_required_and_checked()
    {
        var validator = new UpdatePictureProfileDtoValidator(Uploads);

        validator.TestValidate(new UpdatePictureProfileDto { PictureFile = FormFiles.Create("p.png", FormFiles.Png) }).ShouldNotHaveAnyValidationErrors();
        validator.TestValidate(new UpdatePictureProfileDto { PictureFile = FormFiles.Create("p.png", FormFiles.Jpeg) }).ShouldHaveValidationErrorFor(x => x.PictureFile);
        var missing = validator.TestValidate(new UpdatePictureProfileDto { PictureFile = null! });
        missing.ShouldHaveValidationErrorFor(x => x.PictureFile).WithErrorMessage("Picture file is required.");
        Assert.Single(missing.Errors);
    }

    // ---- videos ----

    [Fact]
    public void Create_video_with_real_mp4_is_valid()
    {
        var dto = new CreateVideoDto { Title = "T", OriginalName = "o.mp4", VideoFile = FormFiles.Create("v.mp4", FormFiles.Mp4) };

        new CreateVideoDtoValidator(Uploads).TestValidate(dto).ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData("v.mp4", "exe")]
    [InlineData("v.avi", "mp4")]
    [InlineData("v.webm", "mp4")]
    public void Create_video_rejects_bad_files(string name, string content)
    {
        var bytes = content == "exe" ? FormFiles.Exe : FormFiles.Mp4;
        var dto = new CreateVideoDto { Title = "T", OriginalName = "o", VideoFile = FormFiles.Create(name, bytes) };

        new CreateVideoDtoValidator(Uploads).TestValidate(dto).ShouldHaveValidationErrorFor(x => x.VideoFile);
    }

    [Fact]
    public void Create_video_oversized_file_is_rejected()
    {
        var dto = new CreateVideoDto { Title = "T", OriginalName = "o", VideoFile = FormFiles.OfSize("v.mp4", FormFiles.Mp4, 1024 * 1024 + 10) };

        new CreateVideoDtoValidator(Uploads).TestValidate(dto).ShouldHaveValidationErrorFor(x => x.VideoFile);
    }

    [Fact]
    public void Create_video_requires_file_title_and_name()
    {
        var result = new CreateVideoDtoValidator(Uploads).TestValidate(new CreateVideoDto { VideoFile = null! });

        result.ShouldHaveValidationErrorFor(x => x.Title);
        result.ShouldHaveValidationErrorFor(x => x.OriginalName);
        result.ShouldHaveValidationErrorFor(x => x.VideoFile).WithErrorMessage("Video file is required.");
    }

    [Fact]
    public void Update_video_file()
    {
        var validator = new UpdateVideoFileDtoValidator(Uploads);

        validator.TestValidate(new UpdateVideoFileDto { VideoFile = FormFiles.Create("v.mp4", FormFiles.Mp4) }).ShouldNotHaveAnyValidationErrors();
        validator.TestValidate(new UpdateVideoFileDto { VideoFile = FormFiles.Create("v.mp4", FormFiles.Png) }).ShouldHaveValidationErrorFor(x => x.VideoFile);
    }

    [Fact]
    public void Update_video_metadata_limits()
    {
        var validator = new UpdateVideoDtoValidator();

        validator.TestValidate(new UpdateVideoDto { Title = null, OriginalName = null }).ShouldNotHaveAnyValidationErrors();
        validator.TestValidate(new UpdateVideoDto { Title = new string('t', 501) }).ShouldHaveValidationErrorFor(x => x.Title);
        validator.TestValidate(new UpdateVideoDto { OriginalName = new string('o', 201) }).ShouldHaveValidationErrorFor(x => x.OriginalName);
    }

    // ---- notifications ----

    [Fact]
    public void Send_email_basic_rules()
    {
        var validator = new SendEmailDtoValidator();

        validator.TestValidate(new SendEmailDto { ToEmail = "a@b.com", Subject = "s", Body = "b" }).ShouldNotHaveAnyValidationErrors();
        var bad = validator.TestValidate(new SendEmailDto());
        bad.ShouldHaveValidationErrorFor(x => x.ToEmail);
        bad.ShouldHaveValidationErrorFor(x => x.Subject);
        bad.ShouldHaveValidationErrorFor(x => x.Body);
    }

    [Fact]
    public void Send_email_attachment_limits()
    {
        var validator = new SendEmailDtoValidator();
        SendEmailDto With(params Microsoft.AspNetCore.Http.IFormFile[] files) => new() { ToEmail = "a@b.com", Subject = "s", Body = "b", Attachments = files.ToList() };

        Assert.True(validator.TestValidate(With(FormFiles.Create("a.txt", [1]))).IsValid);
        Assert.False(validator.TestValidate(With(FormFiles.Create("a.txt", []))).IsValid);
        Assert.False(validator.TestValidate(With(FormFiles.OfSize("a.bin", [], SendEmailDtoValidator.MaxAttachmentBytes + 1))).IsValid);
        Assert.False(validator.TestValidate(With(Enumerable.Range(0, SendEmailDtoValidator.MaxAttachments + 1).Select(_ => FormFiles.Create("a.txt", [1])).ToArray())).IsValid);
        Assert.False(validator.TestValidate(With(
            FormFiles.OfSize("a", [], 4 * 1024 * 1024), FormFiles.OfSize("b", [], 4 * 1024 * 1024), FormFiles.OfSize("c", [], 4 * 1024 * 1024))).IsValid);
    }

    [Theory]
    [InlineData("+201012345678", true)]
    [InlineData("01012345678", true)]
    [InlineData("+15551234567", false)]
    [InlineData("", false)]
    public void Send_sms_phone(string phone, bool valid)
    {
        Assert.Equal(valid, new SendSMSDtoValidator().TestValidate(new SendSMSDto { ToPhoneNumber = phone, Body = "hi" }).IsValid);
    }

    [Fact]
    public void Send_sms_body_limits()
    {
        var validator = new SendSMSDtoValidator();

        validator.TestValidate(new SendSMSDto { ToPhoneNumber = "+201012345678", Body = "" }).ShouldHaveValidationErrorFor(x => x.Body);
        validator.TestValidate(new SendSMSDto { ToPhoneNumber = "+201012345678", Body = new string('b', 201) }).ShouldHaveValidationErrorFor(x => x.Body);
    }
}
