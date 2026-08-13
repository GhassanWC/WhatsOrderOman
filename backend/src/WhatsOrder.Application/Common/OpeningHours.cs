using System.Text.Json;

namespace WhatsOrder.Application.Common;

/// <summary>Day 0 = Sunday … 6 = Saturday (matches .NET DayOfWeek and the Omani week).</summary>
public sealed record OpeningHourItem(int Day, bool Closed, string Open, string Close);

public static class OpeningHours
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    /// <summary>Oman standard time. No DST.</summary>
    public static readonly TimeSpan OmanUtcOffset = TimeSpan.FromHours(4);

    public static List<OpeningHourItem> Defaults() =>
        Enumerable.Range(0, 7).Select(d => new OpeningHourItem(d, false, "09:00", "21:00")).ToList();

    public static string Serialize(IEnumerable<OpeningHourItem> hours) =>
        JsonSerializer.Serialize(Normalize(hours), Json);

    public static List<OpeningHourItem> Parse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return Defaults();
        try
        {
            var parsed = JsonSerializer.Deserialize<List<OpeningHourItem>>(json, Json);
            return parsed is { Count: > 0 } ? Normalize(parsed) : Defaults();
        }
        catch (JsonException)
        {
            return Defaults();
        }
    }

    /// <summary>Exactly one entry per day 0–6, valid HH:mm strings.</summary>
    public static List<OpeningHourItem> Normalize(IEnumerable<OpeningHourItem> hours)
    {
        var byDay = hours.Where(h => h.Day is >= 0 and <= 6)
                         .GroupBy(h => h.Day)
                         .ToDictionary(g => g.Key, g => g.First());

        return Enumerable.Range(0, 7).Select(d =>
        {
            if (!byDay.TryGetValue(d, out var h))
                return new OpeningHourItem(d, false, "09:00", "21:00");
            var open = TimeOnly.TryParse(h.Open, out var o) ? o.ToString("HH:mm") : "09:00";
            var close = TimeOnly.TryParse(h.Close, out var c) ? c.ToString("HH:mm") : "21:00";
            return new OpeningHourItem(d, h.Closed, open, close);
        }).ToList();
    }

    /// <summary>Overnight ranges (e.g. 18:00–02:00) are supported; Open == Close means open 24h.</summary>
    public static bool IsOpenAt(IReadOnlyList<OpeningHourItem> hours, DateTime utcNow, TimeSpan? utcOffset = null)
    {
        var local = utcNow + (utcOffset ?? OmanUtcOffset);
        var today = hours.FirstOrDefault(h => h.Day == (int)local.DayOfWeek);
        var time = TimeOnly.FromDateTime(local);

        // A shop open overnight yesterday (e.g. Mon 18:00–02:00) is still open early Tuesday.
        var yesterdayDay = ((int)local.DayOfWeek + 6) % 7;
        var yesterday = hours.FirstOrDefault(h => h.Day == yesterdayDay);
        if (yesterday is { Closed: false }
            && TimeOnly.TryParse(yesterday.Open, out var yOpen)
            && TimeOnly.TryParse(yesterday.Close, out var yClose)
            && yClose < yOpen && time < yClose)
        {
            return true;
        }

        if (today is null || today.Closed)
            return false;
        if (!TimeOnly.TryParse(today.Open, out var open) || !TimeOnly.TryParse(today.Close, out var close))
            return false;

        if (open == close)
            return true;                       // 24h
        return close > open
            ? time >= open && time < close     // same-day range
            : time >= open;                    // overnight range starting today
    }
}
