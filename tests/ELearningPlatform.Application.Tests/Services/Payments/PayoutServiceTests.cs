namespace ELearningPlatform.Application.Tests.Services.Payments;

public sealed class PayoutServiceTests : IAsyncLifetime
{
    private ServiceTestContext _ctx = null!;
    private IPayoutService Sut => _ctx.Get<IPayoutService>();

    public async ValueTask InitializeAsync() => _ctx = await ServiceTestContext.CreateAsync();
    public ValueTask DisposeAsync() => _ctx.DisposeAsync();

    /// <summary>A teacher who sold one course for 100 (80 to the teacher after the 20% fee).</summary>
    private async Task<Teacher> TeacherWithEarningsAsync()
    {
        var teacher = await _ctx.CreateTeacherAsync();
        var course = await _ctx.CreateCourseAsync(teacher, "Sold", price: 100m);
        var student = await _ctx.CreateStudentAsync();
        await _ctx.FundWalletAsync(student.ApplicationUserId, 100m);
        Assert.True((await _ctx.Get<IPurchaseService>().PurchaseAsync(student.ApplicationUserId, course.Id)).IsSuccess);
        return teacher;
    }

    [Fact]
    public async Task Request_moves_the_amount_to_the_payout_hold()
    {
        var teacher = await TeacherWithEarningsAsync();

        var result = await Sut.RequestAsync(teacher.ApplicationUserId, new RequestPayoutDto { Amount = 50m });

        Assert.True(result.IsSuccess, result.Message);
        Assert.Equal(PayoutStatus.Pending, result.Data!.Status);
        Assert.Equal("Tom Teacher", result.Data.TeacherName);
        Assert.Equal(30m, await _ctx.BalanceAsync(teacher.ApplicationUserId));
        Assert.Equal(50m, await _ctx.SystemBalanceAsync(WalletType.PayoutHold));
        await _ctx.AssertLedgerConsistentAsync();
    }

    [Fact]
    public async Task Request_above_the_balance_is_rejected()
    {
        var teacher = await TeacherWithEarningsAsync();

        var result = await Sut.RequestAsync(teacher.ApplicationUserId, new RequestPayoutDto { Amount = 80.01m });

        Assert.Equal(ErrorCodes.InsufficientFunds, result.Error.Code);
        Assert.Equal(80m, await _ctx.BalanceAsync(teacher.ApplicationUserId));
    }

    [Fact]
    public async Task Students_cannot_request_payouts()
    {
        var student = await _ctx.CreateStudentAsync();
        await _ctx.FundWalletAsync(student.ApplicationUserId, 50m);

        var result = await Sut.RequestAsync(student.ApplicationUserId, new RequestPayoutDto { Amount = 20m });

        Assert.Equal(ErrorCodes.NotATeacher, result.Error.Code);
    }

    [Fact]
    public async Task Approve_sends_the_held_money_out()
    {
        var teacher = await TeacherWithEarningsAsync();
        var payout = (await Sut.RequestAsync(teacher.ApplicationUserId, new RequestPayoutDto { Amount = 80m })).Data!;

        var result = await Sut.ApproveAsync(payout.Id, "admin");

        Assert.True(result.IsSuccess, result.Message);
        Assert.Equal(PayoutStatus.Completed, result.Data!.Status);
        Assert.Equal(_ctx.Now, result.Data.ProcessedAt);
        Assert.Equal(0m, await _ctx.SystemBalanceAsync(WalletType.PayoutHold));
        Assert.Equal(0m, await _ctx.BalanceAsync(teacher.ApplicationUserId));
        await _ctx.AssertLedgerConsistentAsync();
    }

    [Fact]
    public async Task Reject_returns_the_money_to_the_teacher()
    {
        var teacher = await TeacherWithEarningsAsync();
        var payout = (await Sut.RequestAsync(teacher.ApplicationUserId, new RequestPayoutDto { Amount = 60m })).Data!;

        var result = await Sut.RejectAsync(payout.Id, "admin", new RejectPayoutDto { Reason = " Bank details missing " });

        Assert.True(result.IsSuccess, result.Message);
        Assert.Equal((PayoutStatus.Rejected, "Bank details missing"), (result.Data!.Status, result.Data.RejectionReason));
        Assert.Equal(80m, await _ctx.BalanceAsync(teacher.ApplicationUserId));
        Assert.Equal(0m, await _ctx.SystemBalanceAsync(WalletType.PayoutHold));
        await _ctx.AssertLedgerConsistentAsync();
    }

    [Fact]
    public async Task A_processed_payout_cannot_be_processed_again()
    {
        var teacher = await TeacherWithEarningsAsync();
        var payout = (await Sut.RequestAsync(teacher.ApplicationUserId, new RequestPayoutDto { Amount = 10m })).Data!;
        await Sut.ApproveAsync(payout.Id, "admin");

        Assert.Equal(ErrorCodes.PayoutNotPending, (await Sut.ApproveAsync(payout.Id, "admin")).Error.Code);
        Assert.Equal(ErrorCodes.PayoutNotPending, (await Sut.RejectAsync(payout.Id, "admin", new RejectPayoutDto { Reason = "x" })).Error.Code);
        Assert.Equal(ErrorCodes.PayoutNotFound, (await Sut.ApproveAsync(Guid.NewGuid(), "admin")).Error.Code);
    }

    [Fact]
    public async Task Earnings_summarise_sales_and_payouts()
    {
        var teacher = await TeacherWithEarningsAsync();
        var approved = (await Sut.RequestAsync(teacher.ApplicationUserId, new RequestPayoutDto { Amount = 30m })).Data!;
        await Sut.ApproveAsync(approved.Id, "admin");
        await Sut.RequestAsync(teacher.ApplicationUserId, new RequestPayoutDto { Amount = 20m });

        var earnings = (await Sut.GetEarningsAsync(teacher.ApplicationUserId)).Data!;

        Assert.Equal(("USD", 30m, 80m, 20m, 30m, 1), (earnings.Currency, earnings.Balance, earnings.TotalEarned, earnings.PendingPayouts, earnings.PaidOut, earnings.SalesCount));
        var course = Assert.Single(earnings.Courses);
        Assert.Equal(("Sold", 1, 80m), (course.CourseName, course.SalesCount, course.Earned));
    }

    [Fact]
    public async Task Admin_lists_payouts_by_status()
    {
        var teacher = await TeacherWithEarningsAsync();
        var first = (await Sut.RequestAsync(teacher.ApplicationUserId, new RequestPayoutDto { Amount = 10m })).Data!;
        await Sut.RequestAsync(teacher.ApplicationUserId, new RequestPayoutDto { Amount = 10m });
        await Sut.ApproveAsync(first.Id, "admin");

        var pending = (await Sut.GetAllAsync(new PayoutQuery { Status = PayoutStatus.Pending }, "https://x")).Data!;
        var mine = (await Sut.GetMyPayoutsAsync(teacher.ApplicationUserId, new PayoutQuery(), "https://x")).Data!;

        Assert.Equal(1, pending.TotalCount);
        Assert.Equal(2, mine.TotalCount);
    }
}
