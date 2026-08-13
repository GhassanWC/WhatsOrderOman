using FluentAssertions;
using WhatsOrder.Application.Common;
using Xunit;

namespace WhatsOrder.UnitTests;

public class OpeningHoursTests
{
    // 2026-08-09 is a Sunday. 08:00 UTC = 12:00 Oman time.
    private static readonly DateTime SundayNoonUtc = new(2026, 8, 9, 8, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Defaults_cover_all_seven_days()
    {
        var defaults = OpeningHours.Defaults();
        defaults.Should().HaveCount(7);
        defaults.Select(h => h.Day).Should().BeEquivalentTo(Enumerable.Range(0, 7));
    }

    [Fact]
    public void Serialize_then_parse_round_trips()
    {
        var hours = OpeningHours.Defaults();
        var parsed = OpeningHours.Parse(OpeningHours.Serialize(hours));
        parsed.Should().BeEquivalentTo(hours);
    }

    [Fact]
    public void Parse_invalid_json_returns_defaults() =>
        OpeningHours.Parse("not-json").Should().BeEquivalentTo(OpeningHours.Defaults());

    [Fact]
    public void Open_during_regular_hours()
    {
        var hours = OpeningHours.Defaults(); // 09:00–21:00 every day
        OpeningHours.IsOpenAt(hours, SundayNoonUtc).Should().BeTrue();
    }

    [Fact]
    public void Closed_outside_regular_hours()
    {
        var hours = OpeningHours.Defaults();
        var earlyMorningUtc = new DateTime(2026, 8, 9, 1, 0, 0, DateTimeKind.Utc); // 05:00 Oman
        OpeningHours.IsOpenAt(hours, earlyMorningUtc).Should().BeFalse();
    }

    [Fact]
    public void Closed_day_is_closed_even_within_hours()
    {
        var hours = OpeningHours.Defaults()
            .Select(h => h.Day == 0 ? h with { Closed = true } : h).ToList();
        OpeningHours.IsOpenAt(hours, SundayNoonUtc).Should().BeFalse();
    }

    [Fact]
    public void Overnight_range_stays_open_past_midnight()
    {
        // Sunday 18:00–02:00. Monday 00:30 Oman time = Sunday 20:30 UTC.
        var hours = OpeningHours.Defaults()
            .Select(h => h with { Open = "18:00", Close = "02:00" }).ToList();
        var mondayHalfPastMidnightUtc = new DateTime(2026, 8, 9, 20, 30, 0, DateTimeKind.Utc);
        OpeningHours.IsOpenAt(hours, mondayHalfPastMidnightUtc).Should().BeTrue();
    }

    [Fact]
    public void Open_equals_close_means_24_hours()
    {
        var hours = OpeningHours.Defaults()
            .Select(h => h with { Open = "00:00", Close = "00:00" }).ToList();
        var earlyMorningUtc = new DateTime(2026, 8, 9, 1, 0, 0, DateTimeKind.Utc);
        OpeningHours.IsOpenAt(hours, earlyMorningUtc).Should().BeTrue();
    }

    [Fact]
    public void Normalize_fills_missing_days()
    {
        var normalized = OpeningHours.Normalize([new OpeningHourItem(3, true, "10:00", "14:00")]);
        normalized.Should().HaveCount(7);
        normalized.Single(h => h.Day == 3).Closed.Should().BeTrue();
        normalized.Single(h => h.Day == 0).Closed.Should().BeFalse();
    }
}
