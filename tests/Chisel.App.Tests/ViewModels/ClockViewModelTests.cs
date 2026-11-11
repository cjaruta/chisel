using Chisel.App.ViewModels;
using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace Chisel.App.Tests.ViewModels;

public class ClockViewModelTests
{
    [Fact]
    public void Constructor_SetsNowFromTimeProvider()
    {
        var time = new FakeTimeProvider();
        time.SetUtcNow(new DateTimeOffset(2026, 11, 10, 8, 30, 0, TimeSpan.Zero));

        var viewModel = new ClockViewModel(time);

        Assert.Equal(time.GetLocalNow(), viewModel.Now);
    }

    [Fact]
    public void Refresh_AfterTimeAdvances_UpdatesNow()
    {
        var time = new FakeTimeProvider();
        time.SetUtcNow(new DateTimeOffset(2026, 11, 10, 8, 30, 0, TimeSpan.Zero));
        var viewModel = new ClockViewModel(time);

        time.SetUtcNow(new DateTimeOffset(2026, 11, 10, 8, 31, 0, TimeSpan.Zero));
        viewModel.Refresh();

        Assert.Equal(time.GetLocalNow(), viewModel.Now);
    }
}
