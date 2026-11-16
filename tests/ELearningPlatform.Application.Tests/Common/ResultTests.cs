namespace ELearningPlatform.Application.Tests.Common;

public class ResultTests
{
    [Fact]
    public void Success_has_no_error()
    {
        var result = Result.Success("done");

        Assert.True(result.IsSuccess);
        Assert.False(result.IsFailure);
        Assert.Equal("done", result.Message);
        Assert.Equal(Error.None, result.Error);
        Assert.Equal(ErrorType.None, result.ErrorType);
    }

    public static TheoryData<Func<string, string?, Result>, ErrorType> Factories => new()
    {
        { (m, c) => Result.NotFound(m, c), ErrorType.NotFound },
        { (m, c) => Result.Validation(m, c), ErrorType.Validation },
        { (m, c) => Result.Conflict(m, c), ErrorType.Conflict },
        { (m, c) => Result.Unauthorized(m, c), ErrorType.Unauthorized },
        { (m, c) => Result.Forbidden(m, c), ErrorType.Forbidden },
        { (m, c) => Result.Fail(m, ErrorType.Failure, c), ErrorType.Failure },
    };

    [Theory]
    [MemberData(nameof(Factories))]
    public void Failure_factories_set_type_message_and_default_code(Func<string, string?, Result> factory, ErrorType type)
    {
        var result = factory("boom", null);

        Assert.True(result.IsFailure);
        Assert.Equal(type, result.ErrorType);
        Assert.Equal("boom", result.Message);
        Assert.Equal("boom", result.Error.Description);
        Assert.Equal($"General.{type}", result.Error.Code);
    }

    [Theory]
    [MemberData(nameof(Factories))]
    public void Failure_factories_keep_a_custom_code(Func<string, string?, Result> factory, ErrorType _)
    {
        Assert.Equal("My.Code", factory("x", "My.Code").Error.Code);
    }

    [Fact]
    public void Generic_success_carries_data()
    {
        var result = Result<int>.Success(42, "ok");

        Assert.True(result.IsSuccess);
        Assert.Equal(42, result.Data);
        Assert.Equal("ok", result.Message);
    }

    public static TheoryData<Func<string, string?, Result<int>>, ErrorType> GenericFactories => new()
    {
        { (m, c) => Result<int>.NotFound(m, c), ErrorType.NotFound },
        { (m, c) => Result<int>.Validation(m, c), ErrorType.Validation },
        { (m, c) => Result<int>.Conflict(m, c), ErrorType.Conflict },
        { (m, c) => Result<int>.Unauthorized(m, c), ErrorType.Unauthorized },
        { (m, c) => Result<int>.Forbidden(m, c), ErrorType.Forbidden },
        { (m, c) => Result<int>.Fail(m, ErrorType.Failure, c), ErrorType.Failure },
    };

    [Theory]
    [MemberData(nameof(GenericFactories))]
    public void Generic_failures_have_default_data(Func<string, string?, Result<int>> factory, ErrorType type)
    {
        var result = factory("nope", "C.1");

        Assert.Equal(type, result.ErrorType);
        Assert.Equal(default, result.Data);
        Assert.Equal("C.1", result.Error.Code);
    }

    [Fact]
    public void From_carries_a_failure_across_types()
    {
        var failure = Result.NotFound("gone", "X.Gone");

        var converted = Result<string>.From(failure);

        Assert.Equal(failure.Error, converted.Error);
        Assert.Null(converted.Data);
    }

    [Fact]
    public void From_rejects_success()
    {
        Assert.Throws<ArgumentException>(() => Result<string>.From(Result.Success()));
    }

    [Fact]
    public void Fail_with_error_object_uses_its_description()
    {
        var error = Error.Conflict("taken", "Name.Taken");

        var result = Result.Fail(error);

        Assert.Same(error, result.Error);
        Assert.Equal("taken", result.Message);
    }

    [Fact]
    public void Fail_with_none_error_is_rejected()
    {
        Assert.Throws<ArgumentException>(() => Result.Fail(Error.None));
    }

    [Fact]
    public void Errors_compare_by_value()
    {
        Assert.Equal(new Error("A", "b", ErrorType.NotFound), Error.NotFound("b", "A"));
        Assert.NotEqual(Error.NotFound("b", "A"), Error.Conflict("b", "A"));
    }

    [Theory]
    [InlineData(ErrorType.NotFound, "General.NotFound")]
    [InlineData(ErrorType.Validation, "General.Validation")]
    [InlineData(ErrorType.Failure, "General.Failure")]
    public void Default_codes(ErrorType type, string code)
    {
        Assert.Equal(code, Error.DefaultCode(type));
    }

    [Fact]
    public void Error_factories_all_set_their_type()
    {
        Assert.Equal(ErrorType.Failure, Error.Failure("x").Type);
        Assert.Equal(ErrorType.Validation, Error.Validation("x").Type);
        Assert.Equal(ErrorType.NotFound, Error.NotFound("x").Type);
        Assert.Equal(ErrorType.Conflict, Error.Conflict("x").Type);
        Assert.Equal(ErrorType.Unauthorized, Error.Unauthorized("x").Type);
        Assert.Equal(ErrorType.Forbidden, Error.Forbidden("x").Type);
    }

    [Fact]
    public void Error_codes_are_unique()
    {
        var codes = typeof(ErrorCodes).GetFields().Select(f => (string)f.GetValue(null)!).ToList();

        Assert.Equal(codes.Count, codes.Distinct().Count());
        Assert.All(codes, c => Assert.Matches(@"^[A-Z][A-Za-z]+\.[A-Z][A-Za-z]+$", c));
    }
}
