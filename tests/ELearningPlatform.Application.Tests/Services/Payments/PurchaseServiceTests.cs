namespace ELearningPlatform.Application.Tests.Services.Payments;

public sealed class PurchaseServiceTests : IAsyncLifetime
{
    private const string BaseUrl = "https://api.test/api/Purchases";
    private ServiceTestContext _ctx = null!;
    private IPurchaseService Sut => _ctx.Get<IPurchaseService>();

    public async ValueTask InitializeAsync() => _ctx = await ServiceTestContext.CreateAsync();
    public ValueTask DisposeAsync() => _ctx.DisposeAsync();

    [Fact]
    public async Task Purchase_splits_the_price_between_teacher_and_platform_and_enrolls()
    {
        var teacher = await _ctx.CreateTeacherAsync();
        var course = await _ctx.CreateCourseAsync(teacher, "Algebra", price: 50m);
        var student = await _ctx.CreateStudentAsync();
        await _ctx.FundWalletAsync(student.ApplicationUserId, 80m);

        var result = await Sut.PurchaseAsync(student.ApplicationUserId, course.Id);

        Assert.True(result.IsSuccess, result.Message);
        var dto = result.Data!;
        Assert.Equal((50m, 10m, 40m, "USD", PurchaseStatus.Completed, "Algebra"), (dto.Price, dto.PlatformFee, dto.TeacherAmount, dto.Currency, dto.Status, dto.CourseName));
        Assert.Equal(30m, await _ctx.BalanceAsync(student.ApplicationUserId));
        Assert.Equal(40m, await _ctx.BalanceAsync(teacher.ApplicationUserId));
        Assert.Equal(10m, await _ctx.SystemBalanceAsync(WalletType.PlatformRevenue));
        Assert.True(await _ctx.Db.StudentCourses.AnyAsync(sc => sc.StudentId == student.Id && sc.CourseId == course.Id));

        var transaction = await _ctx.Db.LedgerTransactions.AsNoTracking().Include(t => t.Entries)
            .SingleAsync(t => t.Type == LedgerTransactionType.CoursePurchase);
        Assert.Equal(3, transaction.Entries.Count);
        Assert.Equal($"Purchase:{dto.Id}", transaction.Reference);
        await _ctx.AssertLedgerConsistentAsync();
    }

    [Fact]
    public async Task Insufficient_funds_changes_nothing()
    {
        var course = await _ctx.CreateCourseAsync(await _ctx.CreateTeacherAsync(), price: 50m);
        var student = await _ctx.CreateStudentAsync();
        await _ctx.FundWalletAsync(student.ApplicationUserId, 49.99m);

        var result = await Sut.PurchaseAsync(student.ApplicationUserId, course.Id);

        Assert.Equal(ErrorType.Conflict, result.ErrorType);
        Assert.Equal(ErrorCodes.InsufficientFunds, result.Error.Code);
        Assert.Equal(49.99m, await _ctx.BalanceAsync(student.ApplicationUserId));
        Assert.False(await _ctx.Db.CoursePurchases.AnyAsync());
        Assert.False(await _ctx.Db.StudentCourses.AnyAsync());
    }

    [Fact]
    public async Task Student_without_a_wallet_cannot_buy_a_paid_course()
    {
        var course = await _ctx.CreateCourseAsync(await _ctx.CreateTeacherAsync(), price: 5m);
        var student = await _ctx.CreateStudentAsync();

        var result = await Sut.PurchaseAsync(student.ApplicationUserId, course.Id);

        Assert.Equal(ErrorCodes.InsufficientFunds, result.Error.Code);
    }

    [Fact]
    public async Task The_same_course_cannot_be_bought_twice()
    {
        var course = await _ctx.CreateCourseAsync(await _ctx.CreateTeacherAsync(), price: 10m);
        var student = await _ctx.CreateStudentAsync();
        await _ctx.FundWalletAsync(student.ApplicationUserId, 100m);
        await Sut.PurchaseAsync(student.ApplicationUserId, course.Id);

        var second = await Sut.PurchaseAsync(student.ApplicationUserId, course.Id);

        Assert.Equal(ErrorCodes.CourseAlreadyPurchased, second.Error.Code);
        Assert.Equal(90m, await _ctx.BalanceAsync(student.ApplicationUserId));
    }

    [Fact]
    public async Task Free_course_enrolls_without_ledger_entries()
    {
        var course = await _ctx.CreateCourseAsync(await _ctx.CreateTeacherAsync(), price: 0m);
        var student = await _ctx.CreateStudentAsync();

        var result = await Sut.PurchaseAsync(student.ApplicationUserId, course.Id);

        Assert.True(result.IsSuccess, result.Message);
        Assert.Equal(0m, result.Data!.Price);
        Assert.False(await _ctx.Db.LedgerTransactions.AnyAsync());
        Assert.True(await _ctx.Db.StudentCourses.AnyAsync());
    }

    [Fact]
    public async Task Only_students_can_buy()
    {
        var teacher = await _ctx.CreateTeacherAsync();
        var course = await _ctx.CreateCourseAsync(teacher, price: 1m);

        var result = await Sut.PurchaseAsync(teacher.ApplicationUserId, course.Id);

        Assert.Equal(ErrorType.Forbidden, result.ErrorType);
        Assert.Equal(ErrorCodes.NotAStudent, result.Error.Code);
    }

    [Fact]
    public async Task Unknown_course_is_not_found()
    {
        var student = await _ctx.CreateStudentAsync();

        var result = await Sut.PurchaseAsync(student.ApplicationUserId, 999);

        Assert.Equal(ErrorCodes.CourseNotFound, result.Error.Code);
    }

    [Fact]
    public async Task Refund_reverses_the_sale_and_removes_the_enrollment()
    {
        var teacher = await _ctx.CreateTeacherAsync();
        var course = await _ctx.CreateCourseAsync(teacher, price: 25m);
        var student = await _ctx.CreateStudentAsync();
        var admin = await _ctx.CreateUserAsync(RolesConstants.Admin);
        await _ctx.FundWalletAsync(student.ApplicationUserId, 25m);
        var purchase = (await Sut.PurchaseAsync(student.ApplicationUserId, course.Id)).Data!;

        var result = await Sut.RefundAsync(purchase.Id, admin.Id);

        Assert.True(result.IsSuccess, result.Message);
        Assert.Equal(PurchaseStatus.Refunded, result.Data!.Status);
        Assert.Equal(_ctx.Now, result.Data.RefundedAt);
        Assert.Equal(25m, await _ctx.BalanceAsync(student.ApplicationUserId));
        Assert.Equal(0m, await _ctx.BalanceAsync(teacher.ApplicationUserId));
        Assert.Equal(0m, await _ctx.SystemBalanceAsync(WalletType.PlatformRevenue));
        Assert.False(await _ctx.Db.StudentCourses.AnyAsync());
        // Nothing deleted: purchase + its reversal
        Assert.Equal(1, await _ctx.Db.LedgerTransactions.CountAsync(t => t.Type == LedgerTransactionType.CoursePurchase));
        Assert.Equal(1, await _ctx.Db.LedgerTransactions.CountAsync(t => t.Type == LedgerTransactionType.Reversal && t.CreatedByUserId == admin.Id));
        await _ctx.AssertLedgerConsistentAsync();

        // ...and the student can buy it again
        Assert.True((await Sut.PurchaseAsync(student.ApplicationUserId, course.Id)).IsSuccess);
    }

    [Fact]
    public async Task Refund_twice_is_rejected()
    {
        var course = await _ctx.CreateCourseAsync(await _ctx.CreateTeacherAsync(), price: 5m);
        var student = await _ctx.CreateStudentAsync();
        await _ctx.FundWalletAsync(student.ApplicationUserId, 5m);
        var purchase = (await Sut.PurchaseAsync(student.ApplicationUserId, course.Id)).Data!;
        await Sut.RefundAsync(purchase.Id, "admin");

        var second = await Sut.RefundAsync(purchase.Id, "admin");

        Assert.Equal(ErrorCodes.PurchaseNotRefundable, second.Error.Code);
        Assert.Equal(5m, await _ctx.BalanceAsync(student.ApplicationUserId));
    }

    [Fact]
    public async Task Refund_fails_when_the_teacher_already_withdrew_the_money()
    {
        var teacher = await _ctx.CreateTeacherAsync();
        var course = await _ctx.CreateCourseAsync(teacher, price: 50m);
        var student = await _ctx.CreateStudentAsync();
        await _ctx.FundWalletAsync(student.ApplicationUserId, 50m);
        var purchase = (await Sut.PurchaseAsync(student.ApplicationUserId, course.Id)).Data!;
        await _ctx.Get<IPayoutService>().RequestAsync(teacher.ApplicationUserId, new RequestPayoutDto { Amount = 40m });

        var result = await Sut.RefundAsync(purchase.Id, "admin");

        Assert.Equal(ErrorCodes.InsufficientFunds, result.Error.Code);
        Assert.Equal(0m, await _ctx.BalanceAsync(student.ApplicationUserId));
        Assert.Equal(PurchaseStatus.Completed, (await _ctx.Db.CoursePurchases.AsNoTracking().SingleAsync()).Status);
        await _ctx.AssertLedgerConsistentAsync();
    }

    [Fact]
    public async Task Sold_course_cannot_be_deleted()
    {
        var course = await _ctx.CreateCourseAsync(await _ctx.CreateTeacherAsync(), price: 0m);
        var student = await _ctx.CreateStudentAsync();
        await Sut.PurchaseAsync(student.ApplicationUserId, course.Id);

        var result = await _ctx.Get<ICourseService>().DeleteAsync(course.Id);

        Assert.Equal(ErrorCodes.CourseHasPurchases, result.Error.Code);
    }

    [Fact]
    public async Task My_purchases_lists_only_mine_with_filters()
    {
        var teacher = await _ctx.CreateTeacherAsync();
        var a = await _ctx.CreateCourseAsync(teacher, "A", price: 0m);
        var b = await _ctx.CreateCourseAsync(teacher, "B", price: 0m);
        var me = await _ctx.CreateStudentAsync();
        var other = await _ctx.CreateStudentAsync();
        await Sut.PurchaseAsync(me.ApplicationUserId, a.Id);
        await Sut.PurchaseAsync(me.ApplicationUserId, b.Id);
        await Sut.PurchaseAsync(other.ApplicationUserId, a.Id);

        var all = (await Sut.GetMyPurchasesAsync(me.ApplicationUserId, new PurchaseQuery(), BaseUrl)).Data!;
        var onlyB = (await Sut.GetMyPurchasesAsync(me.ApplicationUserId, new PurchaseQuery { CourseId = b.Id }, BaseUrl)).Data!;

        Assert.Equal(2, all.TotalCount);
        Assert.All(all.Items, p => Assert.Equal(me.ApplicationUserId, p.BuyerUserId));
        Assert.Equal("B", Assert.Single(onlyB.Items).CourseName);
    }
}
