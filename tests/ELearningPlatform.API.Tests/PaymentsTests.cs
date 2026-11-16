using ELearningPlatform.API.Tests.Infrastructure;
using System.Net;
using ELearningPlatform.Application.Abstractions.Services;
using ELearningPlatform.Application.Common.Results;
using ELearningPlatform.Application.DTOs.Payments;
using ELearningPlatform.Domain.Enums;

namespace ELearningPlatform.API.Tests;

public class PaymentsTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    [Fact]
    public async Task Full_flow_top_up_purchase_payout_keeps_the_ledger_consistent()
    {
        var teacher = await factory.CreateUserAsync("Teacher");
        var student = await factory.CreateUserAsync("Student");
        var admin = await factory.LoginAdminAsync();
        var course = await teacher.CreateCourseAsync(price: 30m);

        // Top-up: pending until Stripe's webhook arrives
        var topUp = await student.StartTopUpAsync(50m);
        Assert.Equal((PaymentStatus.Pending, "pi_secret_test", "pk_test_api_tests"), (topUp.Status, topUp.ClientSecret, topUp.PublishableKey));
        Assert.Equal(0m, (await student.GetWalletAsync()).Balance);
        await ResponseAssert.StatusAsync(HttpStatusCode.OK,
            await factory.SendStripeEventAsync("payment_intent.succeeded", PaymentHelper.PaymentIntentJson(topUp.PaymentId, 50m)));
        Assert.Equal(50m, (await student.GetWalletAsync()).Balance);

        // Purchase: 20% platform fee
        var purchase = await student.Client.PostIdempotentAsync($"api/Courses/{course.Id}/purchase");
        await ResponseAssert.StatusAsync(HttpStatusCode.Created, purchase);
        var purchaseDto = await purchase.ReadAsync<PurchaseDto>();
        Assert.Equal((30m, 6m, 24m), (purchaseDto.Price, purchaseDto.PlatformFee, purchaseDto.TeacherAmount));
        Assert.Equal(20m, (await student.GetWalletAsync()).Balance);

        // Teacher: earnings and payout
        var earnings = await (await teacher.Client.GetAsync("api/Payouts/earnings")).ReadAsync<TeacherEarningsDto>();
        Assert.Equal((24m, 24m, 1), (earnings.Balance, earnings.TotalEarned, earnings.SalesCount));
        var payoutResponse = await teacher.Client.PostIdempotentAsync("api/Payouts", new { amount = 20m });
        await ResponseAssert.StatusAsync(HttpStatusCode.Created, payoutResponse);
        var payout = await payoutResponse.ReadAsync<PayoutDto>();
        var approve = await admin.Client.PostIdempotentAsync($"api/admin/payouts/{payout.Id}/approve");
        await ResponseAssert.StatusAsync(HttpStatusCode.OK, approve);
        Assert.Equal(PayoutStatus.Completed, (await approve.ReadAsync<PayoutDto>()).Status);
        Assert.Equal(4m, (await teacher.GetWalletAsync()).Balance);

        // Statement and audit trail
        var statement = await (await student.Client.GetAsync("api/Wallet/statement")).ReadAsync<PaginatedResult<StatementLineDto>>();
        Assert.Equal([LedgerTransactionType.CoursePurchase, LedgerTransactionType.TopUp], statement.Items.Select(i => i.Type));
        var audit = await (await admin.Client.GetAsync($"api/admin/audit-logs?entityType=Payout&entityId={payout.Id}")).ReadAsync<PaginatedResult<AuditLogDto>>();
        Assert.Contains(audit.Items, a => a.Action == "Modified" && a.UserId == admin.UserId && a.NewValues!.Contains("Completed"));

        var reconciliation = await (await admin.Client.GetAsync("api/admin/finance/reconciliation")).ReadAsync<ReconciliationDto>();
        Assert.True(reconciliation.IsConsistent);
    }

    [Fact]
    public async Task Retried_top_up_with_the_same_key_returns_the_stored_response()
    {
        var student = await factory.CreateUserAsync("Student");
        var key = Guid.NewGuid().ToString();

        var first = await student.Client.PostIdempotentAsync("api/Wallet/top-ups", new { amount = 15m }, key);
        var second = await student.Client.PostIdempotentAsync("api/Wallet/top-ups", new { amount = 15m }, key);

        await ResponseAssert.StatusAsync(HttpStatusCode.Created, first);
        await ResponseAssert.StatusAsync(HttpStatusCode.Created, second);
        Assert.Equal(await first.Content.ReadAsStringAsync(), await second.Content.ReadAsStringAsync());
        Assert.False(first.Headers.Contains("Idempotent-Replayed"));
        Assert.Equal("true", second.Headers.GetValues("Idempotent-Replayed").Single());
        var payments = await (await student.Client.GetAsync("api/Wallet/top-ups")).ReadAsync<PaginatedResult<PaymentDto>>();
        Assert.Equal(1, payments.TotalCount);
    }

    [Fact]
    public async Task Same_key_with_a_different_request_is_rejected()
    {
        var student = await factory.CreateUserAsync("Student");
        var key = Guid.NewGuid().ToString();
        await student.Client.PostIdempotentAsync("api/Wallet/top-ups", new { amount = 15m }, key);

        var response = await student.Client.PostIdempotentAsync("api/Wallet/top-ups", new { amount = 16m }, key);

        await ResponseAssert.StatusAsync(HttpStatusCode.UnprocessableEntity, response);
        Assert.Equal("Idempotency.KeyReused", await response.ReadProblemCodeAsync());
    }

    [Fact]
    public async Task Keys_are_scoped_per_user()
    {
        var alice = await factory.CreateUserAsync("Student");
        var bob = await factory.CreateUserAsync("Student");
        var key = Guid.NewGuid().ToString();

        await ResponseAssert.StatusAsync(HttpStatusCode.Created, await alice.Client.PostIdempotentAsync("api/Wallet/top-ups", new { amount = 5m }, key));
        var response = await bob.Client.PostIdempotentAsync("api/Wallet/top-ups", new { amount = 5m }, key);

        await ResponseAssert.StatusAsync(HttpStatusCode.Created, response);
        Assert.False(response.Headers.Contains("Idempotent-Replayed"));
    }

    [Fact]
    public async Task Money_endpoints_require_an_idempotency_key()
    {
        var student = await factory.CreateUserAsync("Student");

        var response = await student.Client.PostJsonAsync("api/Wallet/top-ups", new { amount = 5m });

        await ResponseAssert.StatusAsync(HttpStatusCode.BadRequest, response);
        Assert.Equal("Idempotency.KeyMissing", await response.ReadProblemCodeAsync());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(0.5)]
    [InlineData(10000.01)]
    [InlineData(10.123)]
    public async Task Invalid_top_up_amounts_are_rejected(double amount)
    {
        var student = await factory.CreateUserAsync("Student");

        var response = await student.Client.PostIdempotentAsync("api/Wallet/top-ups", new { amount = (decimal)amount });

        await ResponseAssert.StatusAsync(HttpStatusCode.BadRequest, response);
        Assert.Contains("Amount", await response.ReadValidationErrorKeysAsync());
    }

    [Fact]
    public async Task Parallel_purchases_cannot_overspend_the_wallet()
    {
        var teacher = await factory.CreateUserAsync("Teacher");
        var student = await factory.CreateUserAsync("Student");
        var admin = await factory.LoginAdminAsync();
        var courses = new List<int>();
        for (var i = 0; i < 8; i++)
            courses.Add((await teacher.CreateCourseAsync(price: 30m)).Id);
        await factory.TopUpAsync(student, 100m);

        // 8 x 30 requested at once from a balance of 100: exactly 3 may succeed
        var responses = await Task.WhenAll(courses.Select(id => student.Client.PostIdempotentAsync($"api/Courses/{id}/purchase")));

        var statuses = responses.Select(r => r.StatusCode).ToList();
        Assert.Equal(3, statuses.Count(s => s == HttpStatusCode.Created));
        Assert.Equal(5, statuses.Count(s => s == HttpStatusCode.Conflict));
        foreach (var conflict in responses.Where(r => r.StatusCode == HttpStatusCode.Conflict))
            Assert.Contains(await conflict.ReadProblemCodeAsync(), new[] { ErrorCodes.InsufficientFunds, ErrorCodes.ConcurrentUpdate });
        Assert.Equal(10m, (await student.GetWalletAsync()).Balance);
        Assert.True((await (await admin.Client.GetAsync("api/admin/finance/reconciliation")).ReadAsync<ReconciliationDto>()).IsConsistent);
    }

    [Fact]
    public async Task Parallel_purchases_of_the_same_course_charge_once()
    {
        var teacher = await factory.CreateUserAsync("Teacher");
        var student = await factory.CreateUserAsync("Student");
        var course = await teacher.CreateCourseAsync(price: 10m);
        await factory.TopUpAsync(student, 100m);

        var responses = await Task.WhenAll(Enumerable.Range(0, 6).Select(_ => student.Client.PostIdempotentAsync($"api/Courses/{course.Id}/purchase")));

        Assert.Equal(1, responses.Count(r => r.StatusCode == HttpStatusCode.Created));
        Assert.Equal(90m, (await student.GetWalletAsync()).Balance);
    }

    [Fact]
    public async Task Webhook_with_a_bad_signature_is_rejected_and_credits_nothing()
    {
        var student = await factory.CreateUserAsync("Student");
        var topUp = await student.StartTopUpAsync(40m);

        var response = await factory.SendStripeEventAsync("payment_intent.succeeded", PaymentHelper.PaymentIntentJson(topUp.PaymentId, 40m), secret: "whsec_forged");

        await ResponseAssert.StatusAsync(HttpStatusCode.BadRequest, response);
        Assert.Equal("Webhook.InvalidSignature", await response.ReadProblemCodeAsync());
        Assert.Equal(0m, (await student.GetWalletAsync()).Balance);
    }

    [Fact]
    public async Task Redelivered_webhook_credits_once()
    {
        var student = await factory.CreateUserAsync("Student");
        var topUp = await student.StartTopUpAsync(40m);
        var eventId = "evt_" + Guid.NewGuid().ToString("N");

        for (var i = 0; i < 3; i++)
            await ResponseAssert.StatusAsync(HttpStatusCode.OK,
                await factory.SendStripeEventAsync("payment_intent.succeeded", PaymentHelper.PaymentIntentJson(topUp.PaymentId, 40m), eventId));

        Assert.Equal(40m, (await student.GetWalletAsync()).Balance);
    }

    [Fact]
    public async Task Admin_refunds_a_purchase()
    {
        var teacher = await factory.CreateUserAsync("Teacher");
        var student = await factory.CreateUserAsync("Student");
        var admin = await factory.LoginAdminAsync();
        var course = await teacher.CreateCourseAsync(price: 25m);
        await factory.TopUpAsync(student, 25m);
        var purchase = await (await student.Client.PostIdempotentAsync($"api/Courses/{course.Id}/purchase")).ReadAsync<PurchaseDto>();

        var refund = await admin.Client.PostIdempotentAsync($"api/admin/purchases/{purchase.Id}/refund");

        await ResponseAssert.StatusAsync(HttpStatusCode.OK, refund);
        Assert.Equal(PurchaseStatus.Refunded, (await refund.ReadAsync<PurchaseDto>()).Status);
        Assert.Equal(25m, (await student.GetWalletAsync()).Balance);
        Assert.Equal(0m, (await teacher.GetWalletAsync()).Balance);
    }

    [Fact]
    public async Task Admin_refunds_a_top_up_through_the_provider()
    {
        var student = await factory.CreateUserAsync("Student");
        var admin = await factory.LoginAdminAsync();
        var topUp = await factory.TopUpAsync(student, 12m);
        factory.Payments.RefundAsync(Arg.Is<RefundRequest>(r => r.PaymentId == topUp.PaymentId), Arg.Any<CancellationToken>())
            .Returns(new ProviderRefund("re_1", "succeeded"));

        var refund = await admin.Client.PostIdempotentAsync($"api/admin/payments/{topUp.PaymentId}/refund");

        await ResponseAssert.StatusAsync(HttpStatusCode.OK, refund);
        Assert.Equal(PaymentStatus.Refunded, (await refund.ReadAsync<PaymentDto>()).Status);
        Assert.Equal(0m, (await student.GetWalletAsync()).Balance);
    }

    [Fact]
    public async Task Statement_exports_as_csv()
    {
        var student = await factory.CreateUserAsync("Student");
        await factory.TopUpAsync(student, 7.5m);

        var response = await student.Client.GetAsync("api/Wallet/statement/export");

        await ResponseAssert.StatusAsync(HttpStatusCode.OK, response);
        Assert.Equal("text/csv", response.Content.Headers.ContentType!.MediaType);
        Assert.EndsWith(".csv", response.Content.Headers.ContentDisposition!.FileNameStar ?? response.Content.Headers.ContentDisposition.FileName);
        Assert.Contains(",TopUp,Credit,7.50,USD,7.50,", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Config_exposes_only_the_publishable_key()
    {
        var student = await factory.CreateUserAsync("Student");

        var body = await (await student.Client.GetAsync("api/Payments/config")).Content.ReadAsStringAsync();

        Assert.Contains("pk_test_api_tests", body);
        Assert.DoesNotContain("sk_test", body);
        Assert.DoesNotContain("whsec", body);
    }

    [Theory]
    [InlineData("Teacher", "POST", "api/Wallet/top-ups")]
    [InlineData("Student", "POST", "api/Payouts")]
    [InlineData("Student", "GET", "api/Payouts/earnings")]
    [InlineData("Teacher", "POST", "api/Courses/1/purchase")]
    [InlineData("Student", "GET", "api/admin/payments")]
    [InlineData("Teacher", "GET", "api/admin/audit-logs")]
    [InlineData("Student", "POST", "api/admin/payouts/00000000-0000-0000-0000-000000000001/approve")]
    public async Task Roles_are_enforced(string role, string method, string url)
    {
        var user = await factory.CreateUserAsync(role);
        var request = new HttpRequestMessage(new HttpMethod(method), url);
        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());

        var response = await user.Client.SendAsync(request);

        await ResponseAssert.StatusAsync(HttpStatusCode.Forbidden, response);
    }

    [Fact]
    public async Task Wallet_requires_authentication()
        => await ResponseAssert.StatusAsync(HttpStatusCode.Unauthorized, await factory.CreateHttpsClient().GetAsync("api/Wallet"));
}

/// <summary>A factory with a low payments rate limit.</summary>
public sealed class LowPaymentsLimitFactory : ApiFactory
{
    protected override IDictionary<string, string?> Settings
    {
        get
        {
            var settings = base.Settings;
            settings["RateLimiting:PaymentsPermitLimit"] = "2";
            return settings;
        }
    }
}

public class PaymentsRateLimitTests(LowPaymentsLimitFactory factory) : IClassFixture<LowPaymentsLimitFactory>
{
    [Fact]
    public async Task Payment_endpoints_are_rate_limited_per_user()
    {
        var student = await factory.CreateUserAsync("Student");
        var other = await factory.CreateUserAsync("Student");

        var statuses = new List<HttpStatusCode>();
        for (var i = 0; i < 3; i++)
            statuses.Add((await student.Client.PostIdempotentAsync("api/Wallet/top-ups", new { amount = 5m })).StatusCode);

        Assert.Equal([HttpStatusCode.Created, HttpStatusCode.Created, HttpStatusCode.TooManyRequests], statuses);
        // Another user has their own budget
        await ResponseAssert.StatusAsync(HttpStatusCode.Created, await other.Client.PostIdempotentAsync("api/Wallet/top-ups", new { amount = 5m }));
    }
}
