using System.Text.Json;

namespace ELearningPlatform.Application.Tests.Services.Payments;

public sealed class AuditLogTests : IAsyncLifetime
{
    private ServiceTestContext _ctx = null!;

    public async ValueTask InitializeAsync() => _ctx = await ServiceTestContext.CreateAsync();
    public ValueTask DisposeAsync() => _ctx.DisposeAsync();

    [Fact]
    public async Task Changes_record_who_what_when_and_old_and_new_values()
    {
        _ctx.CurrentUser.UserId.Returns("admin-7");
        _ctx.CurrentUser.IpAddress.Returns("10.0.0.1");
        var course = await _ctx.CreateCourseAsync(await _ctx.CreateTeacherAsync(), price: 10m);

        await _ctx.Get<ICourseService>().UpdatePriceAsync(course.Id, new UpdateCoursePriceDto { NewPrice = 12.5m });

        var log = await _ctx.Db.AuditLogs.AsNoTracking().SingleAsync(a => a.EntityType == nameof(Course));
        Assert.Equal(("admin-7", "Modified", course.Id.ToString(), "10.0.0.1", _ctx.Now), (log.UserId, log.Action, log.EntityId, log.IpAddress, log.Timestamp));
        Assert.Equal(10m, JsonDocument.Parse(log.OldValues!).RootElement.GetProperty("Price").GetDecimal());
        Assert.Equal(12.5m, JsonDocument.Parse(log.NewValues!).RootElement.GetProperty("Price").GetDecimal());
        // Only what changed
        Assert.False(JsonDocument.Parse(log.NewValues!).RootElement.TryGetProperty("Name", out _));
    }

    [Fact]
    public async Task Money_operations_are_audited_in_the_same_save()
    {
        var teacher = await _ctx.CreateTeacherAsync();
        var course = await _ctx.CreateCourseAsync(teacher, price: 10m);
        var student = await _ctx.CreateStudentAsync();
        await _ctx.FundWalletAsync(student.ApplicationUserId, 10m);

        var purchase = (await _ctx.Get<IPurchaseService>().PurchaseAsync(student.ApplicationUserId, course.Id)).Data!;

        var logs = await _ctx.Db.AuditLogs.AsNoTracking().ToListAsync();
        var added = Assert.Single(logs, a => a.EntityType == nameof(CoursePurchase));
        Assert.Equal(("Added", purchase.Id.ToString(), null as string), (added.Action, added.EntityId, added.OldValues));
        Assert.Equal("Completed", JsonDocument.Parse(added.NewValues!).RootElement.GetProperty("Status").GetString());

        var walletChange = logs.Last(a => a.EntityType == nameof(Wallet) && a.Action == "Modified");
        Assert.Contains("\"Balance\"", walletChange.NewValues);
    }

    [Fact]
    public async Task Audit_log_is_append_only()
    {
        var course = await _ctx.CreateCourseAsync(await _ctx.CreateTeacherAsync(), price: 10m);
        await _ctx.Get<ICourseService>().UpdatePriceAsync(course.Id, new UpdateCoursePriceDto { NewPrice = 11m });

        _ctx.Db.AuditLogs.Remove(await _ctx.Db.AuditLogs.FirstAsync());

        await Assert.ThrowsAsync<InvalidOperationException>(() => _ctx.Db.SaveChangesAsync());
    }

    [Fact]
    public async Task Audit_logs_can_be_filtered_by_entity()
    {
        var course = await _ctx.CreateCourseAsync(await _ctx.CreateTeacherAsync(), price: 10m);
        await _ctx.Get<ICourseService>().UpdatePriceAsync(course.Id, new UpdateCoursePriceDto { NewPrice = 11m });
        await _ctx.Get<ICourseService>().UpdatePriceAsync(course.Id, new UpdateCoursePriceDto { NewPrice = 12m });
        await _ctx.FundWalletAsync((await _ctx.CreateStudentAsync()).ApplicationUserId, 5m);

        var page = (await _ctx.Get<IAuditLogService>().GetAsync(
            new AuditLogQuery { EntityType = nameof(Course), EntityId = course.Id.ToString() }, "https://x")).Data!;

        Assert.Equal(2, page.TotalCount);
        Assert.Contains("12", page.Items[0].NewValues);
    }
}
