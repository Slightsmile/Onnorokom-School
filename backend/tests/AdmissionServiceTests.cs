using Backend;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace Backend.Tests;

public class AdmissionServiceTests : IAsyncLifetime
{
    private AdmissionService _svc = null!;

    public async Task InitializeAsync()
    {
        Environment.SetEnvironmentVariable("CONNECTION_STRING",
            Environment.GetEnvironmentVariable("TEST_CONNECTION_STRING")
                ?? "Host=localhost;Port=5432;Database=admissions;Username=postgres;Password=postgres");
        Environment.SetEnvironmentVariable("INIT_SQL_PATH",
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "init.sql"));
        Environment.SetEnvironmentVariable("HOLD_DURATION_SECONDS", "600");

        var config = new ConfigurationBuilder().Build();
        var db = new Db(config);
        await db.EnsureSchemaAsync();
        _svc = new AdmissionService(db);
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task CreateHold_SucceedsWhenSeatsAvailable_FailsWhenFull()
    {
        var cls = await _svc.CreateClassAsync("Test Class A " + Guid.NewGuid(), totalSeats: 1);

        var hold = await _svc.CreateHoldAsync(cls.Id, "student-1");
        Assert.Equal("HELD", hold.Status);

        await Assert.ThrowsAsync<SeatFullException>(() => _svc.CreateHoldAsync(cls.Id, "student-2"));
    }

    [Fact]
    public async Task Confirm_IsIdempotent_AndDoesNotDoubleCountSeats()
    {
        var cls = await _svc.CreateClassAsync("Test Class B " + Guid.NewGuid(), totalSeats: 1);
        var hold = await _svc.CreateHoldAsync(cls.Id, "student-1");

        var confirmed1 = await _svc.ConfirmHoldAsync(hold.HoldId, "student-1");
        var confirmed2 = await _svc.ConfirmHoldAsync(hold.HoldId, "student-1");

        Assert.Equal("CONFIRMED", confirmed1.Status);
        Assert.Equal("CONFIRMED", confirmed2.Status);

        var updated = await _svc.GetClassAsync(cls.Id);
        Assert.Equal(1, updated!.ConfirmedSeats);
        Assert.Equal(0, updated.AvailableSeats);
    }

    [Fact]
    public async Task ConcurrentHolds_NeverExceedTotalSeats()
    {
        const int totalSeats = 5;
        const int attempts = 30;
        var cls = await _svc.CreateClassAsync("Concurrency Test " + Guid.NewGuid(), totalSeats);

        var tasks = Enumerable.Range(0, attempts).Select(async i =>
        {
            try
            {
                await _svc.CreateHoldAsync(cls.Id, $"student-{i}");
                return true;
            }
            catch (SeatFullException)
            {
                return false;
            }
        });

        var results = await Task.WhenAll(tasks);
        var successCount = results.Count(r => r);

        Assert.Equal(totalSeats, successCount);

        var final = await _svc.GetClassAsync(cls.Id);
        Assert.Equal(totalSeats, final!.HeldSeats);
        Assert.Equal(0, final.AvailableSeats);
    }
}
