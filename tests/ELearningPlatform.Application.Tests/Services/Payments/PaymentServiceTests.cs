namespace ELearningPlatform.Application.Tests.Services.Payments;

public sealed class PaymentServiceTests : IAsyncLifetime
{
    private ServiceTestContext _ctx = null!;
    private IPaymentService Sut => _ctx.Get<IPaymentService>();

    public async ValueTask InitializeAsync()
    {
        _ctx = await ServiceTestContext.CreateAsync();
        _ctx.Payments.CreatePaymentIntentAsync(Arg.Any<CreatePaymentIntentRequest>(), Arg.Any<CancellationToken>())
            .Returns(call => new ProviderPaymentIntent($"pi_{call.Arg<CreatePaymentIntentRequest>().PaymentId:N}", "secret_123"));
        _ctx.Payments.RefundAsync(Arg.Any<RefundRequest>(), Arg.Any<CancellationToken>())
            .Returns(new ProviderRefund("re_1", "succeeded"));
    }

    public ValueTask DisposeAsync() => _ctx.DisposeAsync();

    private async Task<(Student Student, TopUpDto TopUp)> CreateTopUpAsync(decimal amount = 30m)
    {
        var student = await _ctx.CreateStudentAsync();
        var result = await Sut.CreateTopUpAsync(student.ApplicationUserId, new CreateTopUpDto { Amount = amount });
        Assert.True(result.IsSuccess, result.Message);
        return (student, result.Data!);
    }

    private static PaymentWebhookEvent Succeeded(TopUpDto topUp, decimal? amount = null, string eventId = "evt_1")
        => new(eventId, "payment_intent.succeeded", PaymentWebhookKind.PaymentSucceeded, $"pi_{topUp.PaymentId:N}", amount ?? topUp.Amount, "USD");

    [Fact]
    public async Task Top_up_creates_a_pending_payment_and_a_payment_intent()
    {
        var (student, topUp) = await CreateTopUpAsync(30m);

        Assert.Equal(PaymentStatus.Pending, topUp.Status);
        Assert.Equal("secret_123", topUp.ClientSecret);
        Assert.Equal("pk_test_app_tests", topUp.PublishableKey);
        var payment = await _ctx.Db.Payments.AsNoTracking().SingleAsync();
        Assert.Equal($"pi_{payment.Id:N}", payment.ProviderPaymentId);
        Assert.Equal(student.ApplicationUserId, payment.UserId);
        await _ctx.Payments.Received(1).CreatePaymentIntentAsync(
            Arg.Is<CreatePaymentIntentRequest>(r => r.Amount == 30m && r.Currency == "USD" && r.IdempotencyKey == $"topup-{payment.Id}"),
            Arg.Any<CancellationToken>());
        // Not credited until the provider confirms
        Assert.Equal(0m, await _ctx.BalanceAsync(student.ApplicationUserId));
    }

    [Fact]
    public async Task Provider_failure_marks_the_payment_failed_and_reports_unavailable()
    {
        _ctx.Payments.CreatePaymentIntentAsync(Arg.Any<CreatePaymentIntentRequest>(), Arg.Any<CancellationToken>())
            .Returns<ProviderPaymentIntent>(_ => throw new PaymentProviderException("down"));
        var student = await _ctx.CreateStudentAsync();

        var result = await Sut.CreateTopUpAsync(student.ApplicationUserId, new CreateTopUpDto { Amount = 10m });

        Assert.Equal(ErrorType.Unavailable, result.ErrorType);
        Assert.Equal(ErrorCodes.PaymentProviderError, result.Error.Code);
        Assert.Equal(PaymentStatus.Failed, (await _ctx.Db.Payments.AsNoTracking().SingleAsync()).Status);
    }

    [Fact]
    public async Task Succeeded_webhook_credits_the_wallet()
    {
        var (student, topUp) = await CreateTopUpAsync(30m);

        var result = await Sut.HandleWebhookAsync(Succeeded(topUp));

        Assert.True(result.IsSuccess, result.Message);
        Assert.Equal(30m, await _ctx.BalanceAsync(student.ApplicationUserId));
        var payment = await _ctx.Db.Payments.AsNoTracking().SingleAsync();
        Assert.Equal(PaymentStatus.Succeeded, payment.Status);
        Assert.NotNull(payment.LedgerTransactionId);
        await _ctx.AssertLedgerConsistentAsync();
    }

    [Fact]
    public async Task Duplicate_webhook_delivery_credits_only_once()
    {
        var (student, topUp) = await CreateTopUpAsync(30m);

        await Sut.HandleWebhookAsync(Succeeded(topUp));
        await Sut.HandleWebhookAsync(Succeeded(topUp));                      // same event redelivered
        await Sut.HandleWebhookAsync(Succeeded(topUp, eventId: "evt_other")); // different event, same payment

        Assert.Equal(30m, await _ctx.BalanceAsync(student.ApplicationUserId));
        Assert.Equal(1, await _ctx.Db.LedgerTransactions.CountAsync());
        Assert.Equal(2, await _ctx.Db.ProcessedWebhookEvents.CountAsync());
    }

    [Fact]
    public async Task Amount_mismatch_is_not_credited()
    {
        var (student, topUp) = await CreateTopUpAsync(30m);

        await Sut.HandleWebhookAsync(Succeeded(topUp, amount: 3m));

        Assert.Equal(0m, await _ctx.BalanceAsync(student.ApplicationUserId));
        Assert.Equal(PaymentStatus.Failed, (await _ctx.Db.Payments.AsNoTracking().SingleAsync()).Status);
    }

    [Fact]
    public async Task Failed_webhook_marks_failed_and_a_later_success_still_credits()
    {
        var (student, topUp) = await CreateTopUpAsync(30m);

        await Sut.HandleWebhookAsync(new PaymentWebhookEvent("evt_f", "payment_intent.payment_failed", PaymentWebhookKind.PaymentFailed,
            $"pi_{topUp.PaymentId:N}", 30m, "USD", "Your card was declined."));
        var failed = await _ctx.Db.Payments.AsNoTracking().SingleAsync();
        Assert.Equal((PaymentStatus.Failed, "Your card was declined."), (failed.Status, failed.FailureReason));

        await Sut.HandleWebhookAsync(Succeeded(topUp, eventId: "evt_s"));

        Assert.Equal(PaymentStatus.Succeeded, (await _ctx.Db.Payments.AsNoTracking().SingleAsync()).Status);
        Assert.Equal(30m, await _ctx.BalanceAsync(student.ApplicationUserId));
    }

    [Fact]
    public async Task Webhook_for_unknown_payment_or_ignored_type_is_acknowledged()
    {
        var unknown = await Sut.HandleWebhookAsync(new PaymentWebhookEvent("evt_u", "payment_intent.succeeded", PaymentWebhookKind.PaymentSucceeded, "pi_unknown", 1m, "USD"));
        var ignored = await Sut.HandleWebhookAsync(new PaymentWebhookEvent("evt_i", "customer.created", PaymentWebhookKind.Ignored));

        Assert.True(unknown.IsSuccess);
        Assert.True(ignored.IsSuccess);
        Assert.False(await _ctx.Db.LedgerTransactions.AnyAsync());
    }

    [Fact]
    public async Task Admin_refund_takes_the_money_back_and_refunds_at_the_provider()
    {
        var (student, topUp) = await CreateTopUpAsync(30m);
        await Sut.HandleWebhookAsync(Succeeded(topUp));

        var result = await Sut.RefundAsync(topUp.PaymentId, "admin-1");

        Assert.True(result.IsSuccess, result.Message);
        Assert.Equal(PaymentStatus.Refunded, result.Data!.Status);
        Assert.Equal(0m, await _ctx.BalanceAsync(student.ApplicationUserId));
        await _ctx.Payments.Received(1).RefundAsync(Arg.Is<RefundRequest>(r => r.ProviderPaymentId == $"pi_{topUp.PaymentId:N}" && r.Amount == 30m), Arg.Any<CancellationToken>());
        Assert.Equal(2, await _ctx.Db.LedgerTransactions.CountAsync());
        await _ctx.AssertLedgerConsistentAsync();

        // The charge.refunded webhook that follows changes nothing
        await Sut.HandleWebhookAsync(new PaymentWebhookEvent("evt_r", "charge.refunded", PaymentWebhookKind.PaymentRefunded, $"pi_{topUp.PaymentId:N}", 30m, "USD"));
        Assert.Equal(2, await _ctx.Db.LedgerTransactions.CountAsync());
    }

    [Fact]
    public async Task Refund_of_spent_money_is_rejected_before_calling_the_provider()
    {
        var (student, topUp) = await CreateTopUpAsync(30m);
        await Sut.HandleWebhookAsync(Succeeded(topUp));
        var course = await _ctx.CreateCourseAsync(await _ctx.CreateTeacherAsync(), price: 20m);
        await _ctx.Get<IPurchaseService>().PurchaseAsync(student.ApplicationUserId, course.Id);

        var result = await Sut.RefundAsync(topUp.PaymentId, "admin");

        Assert.Equal(ErrorCodes.InsufficientFunds, result.Error.Code);
        await _ctx.Payments.DidNotReceiveWithAnyArgs().RefundAsync(default!, default);
        Assert.Equal(PaymentStatus.Succeeded, (await _ctx.Db.Payments.AsNoTracking().SingleAsync()).Status);
    }

    [Fact]
    public async Task Provider_refund_failure_is_compensated()
    {
        _ctx.Payments.RefundAsync(Arg.Any<RefundRequest>(), Arg.Any<CancellationToken>())
            .Returns<ProviderRefund>(_ => throw new PaymentProviderException("down"));
        var (student, topUp) = await CreateTopUpAsync(30m);
        await Sut.HandleWebhookAsync(Succeeded(topUp));

        var result = await Sut.RefundAsync(topUp.PaymentId, "admin");

        Assert.Equal(ErrorType.Unavailable, result.ErrorType);
        Assert.Equal(30m, await _ctx.BalanceAsync(student.ApplicationUserId));
        Assert.Equal(PaymentStatus.Succeeded, (await _ctx.Db.Payments.AsNoTracking().SingleAsync()).Status);
        // top-up, reversal, reversal of the reversal: nothing deleted
        Assert.Equal(3, await _ctx.Db.LedgerTransactions.CountAsync());
        await _ctx.AssertLedgerConsistentAsync();

        // A later retry works
        _ctx.Payments.RefundAsync(Arg.Any<RefundRequest>(), Arg.Any<CancellationToken>()).Returns(new ProviderRefund("re_2", "succeeded"));
        Assert.True((await Sut.RefundAsync(topUp.PaymentId, "admin")).IsSuccess);
        Assert.Equal(0m, await _ctx.BalanceAsync(student.ApplicationUserId));
    }

    [Fact]
    public async Task Pending_payment_cannot_be_refunded()
    {
        var (_, topUp) = await CreateTopUpAsync();

        var result = await Sut.RefundAsync(topUp.PaymentId, "admin");

        Assert.Equal(ErrorCodes.PaymentNotRefundable, result.Error.Code);
    }

    [Fact]
    public async Task Refund_from_the_provider_dashboard_is_recorded_even_if_spent()
    {
        var (student, topUp) = await CreateTopUpAsync(30m);
        await Sut.HandleWebhookAsync(Succeeded(topUp));
        var course = await _ctx.CreateCourseAsync(await _ctx.CreateTeacherAsync(), price: 20m);
        await _ctx.Get<IPurchaseService>().PurchaseAsync(student.ApplicationUserId, course.Id);

        await Sut.HandleWebhookAsync(new PaymentWebhookEvent("evt_r", "charge.refunded", PaymentWebhookKind.PaymentRefunded, $"pi_{topUp.PaymentId:N}", 30m, "USD"));

        Assert.Equal(-20m, await _ctx.BalanceAsync(student.ApplicationUserId));
        Assert.Equal(PaymentStatus.Refunded, (await _ctx.Db.Payments.AsNoTracking().SingleAsync()).Status);
        await _ctx.AssertLedgerConsistentAsync();
    }

    [Fact]
    public async Task Users_see_only_their_own_payments()
    {
        var (mine, topUp) = await CreateTopUpAsync();
        var (other, _) = await CreateTopUpAsync();

        Assert.True((await Sut.GetMyPaymentAsync(mine.ApplicationUserId, topUp.PaymentId)).IsSuccess);
        Assert.Equal(ErrorType.NotFound, (await Sut.GetMyPaymentAsync(other.ApplicationUserId, topUp.PaymentId)).ErrorType);
        Assert.Equal(1, (await Sut.GetMyPaymentsAsync(mine.ApplicationUserId, new PaymentQuery(), "https://x")).Data!.TotalCount);
        Assert.Equal(2, (await Sut.GetAllAsync(new PaymentQuery(), "https://x")).Data!.TotalCount);
        Assert.Equal(0, (await Sut.GetAllAsync(new PaymentQuery { Status = PaymentStatus.Succeeded }, "https://x")).Data!.TotalCount);
    }
}
