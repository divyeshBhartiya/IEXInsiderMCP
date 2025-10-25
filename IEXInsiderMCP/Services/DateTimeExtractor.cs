using System.Text.RegularExpressions;

namespace IEXInsiderMCP.Services;

/// <summary>
/// Comprehensive date/time extraction from natural language queries
/// Handles: years, months, days, date ranges, time slots
/// </summary>
public static class DateTimeExtractor
{
    private static readonly Dictionary<string, int> MonthNames = new(StringComparer.OrdinalIgnoreCase)
    {
        { "jan", 1 }, { "january", 1 },
        { "feb", 2 }, { "february", 2 },
        { "mar", 3 }, { "march", 3 },
        { "apr", 4 }, { "april", 4 },
        { "may", 5 },
        { "jun", 6 }, { "june", 6 },
        { "jul", 7 }, { "july", 7 },
        { "aug", 8 }, { "august", 8 },
        { "sep", 9 }, { "sept", 9 }, { "september", 9 },
        { "oct", 10 }, { "october", 10 },
        { "nov", 11 }, { "november", 11 },
        { "dec", 12 }, { "december", 12 }
    };

    public class ExtractedDateInfo
    {
        public int? Year { get; set; }
        public int? Month { get; set; }
        public int? Day { get; set; }
        public int? Quarter { get; set; }
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public string? TimeBlockStart { get; set; }
        public string? TimeBlockEnd { get; set; }
        public int? DaysCount { get; set; }
    }

    /// <summary>
    /// Extract all date/time information from a query
    /// </summary>
    public static ExtractedDateInfo ExtractDateTimeInfo(string query)
    {
        var info = new ExtractedDateInfo();
        var queryLower = query.ToLower();

        // Extract year (2023-2039)
        var yearMatch = Regex.Match(query, @"\b(202[3-9]|203[0-9])\b");
        if (yearMatch.Success)
        {
            info.Year = int.Parse(yearMatch.Value);
        }

        // Extract quarter: Q1, Q2, Q3, Q4
        var quarterMatch = Regex.Match(queryLower, @"\bq([1-4])\b");
        if (quarterMatch.Success)
        {
            info.Quarter = int.Parse(quarterMatch.Groups[1].Value);
        }

        // Extract month name or number
        // Pattern: "September 2025", "Sept 2025", "Sep 2025", "9/2025", "09/2025"
        var monthYearMatch = Regex.Match(query, @"\b(jan|january|feb|february|mar|march|apr|april|may|jun|june|jul|july|aug|august|sep|sept|september|oct|october|nov|november|dec|december)\s+(\d{4})\b", RegexOptions.IgnoreCase);
        if (monthYearMatch.Success)
        {
            var monthStr = monthYearMatch.Groups[1].Value;
            if (MonthNames.TryGetValue(monthStr, out int month))
            {
                info.Month = month;
                info.Year = int.Parse(monthYearMatch.Groups[2].Value);
            }
        }
        else
        {
            // Try standalone month name
            var monthMatch = Regex.Match(queryLower, @"\b(jan|january|feb|february|mar|march|apr|april|may|jun|june|jul|july|aug|august|sep|sept|september|oct|october|nov|november|dec|december)\b");
            if (monthMatch.Success)
            {
                if (MonthNames.TryGetValue(monthMatch.Value, out int month))
                {
                    info.Month = month;
                }
            }
        }

        // Extract specific day: "March 15", "15th March", "15 March 2024"
        var dayMonthMatch = Regex.Match(query, @"\b(\d{1,2})(st|nd|rd|th)?\s+(jan|january|feb|february|mar|march|apr|april|may|jun|june|jul|july|aug|august|sep|sept|september|oct|october|nov|november|dec|december)\b", RegexOptions.IgnoreCase);
        if (dayMonthMatch.Success)
        {
            info.Day = int.Parse(dayMonthMatch.Groups[1].Value);
            var monthStr = dayMonthMatch.Groups[3].Value;
            if (MonthNames.TryGetValue(monthStr, out int month))
            {
                info.Month = month;
            }
        }
        else
        {
            var monthDayMatch = Regex.Match(query, @"\b(jan|january|feb|february|mar|march|apr|april|may|jun|june|jul|july|aug|august|sep|sept|september|oct|october|nov|november|dec|december)\s+(\d{1,2})(st|nd|rd|th)?\b", RegexOptions.IgnoreCase);
            if (monthDayMatch.Success)
            {
                var monthStr = monthDayMatch.Groups[1].Value;
                if (MonthNames.TryGetValue(monthStr, out int month))
                {
                    info.Month = month;
                }
                info.Day = int.Parse(monthDayMatch.Groups[2].Value);
            }
        }

        // Extract date in format: "2024-03-15", "2024/03/15", "15-03-2024", "15/03/2024"
        var isoDateMatch = Regex.Match(query, @"\b(\d{4})[-/](\d{1,2})[-/](\d{1,2})\b");
        if (isoDateMatch.Success)
        {
            info.Year = int.Parse(isoDateMatch.Groups[1].Value);
            info.Month = int.Parse(isoDateMatch.Groups[2].Value);
            info.Day = int.Parse(isoDateMatch.Groups[3].Value);
        }
        else
        {
            var dmyDateMatch = Regex.Match(query, @"\b(\d{1,2})[-/](\d{1,2})[-/](\d{4})\b");
            if (dmyDateMatch.Success)
            {
                info.Day = int.Parse(dmyDateMatch.Groups[1].Value);
                info.Month = int.Parse(dmyDateMatch.Groups[2].Value);
                info.Year = int.Parse(dmyDateMatch.Groups[3].Value);
            }
        }

        // Extract "last X days": "last 7 days", "past 30 days"
        var daysCountMatch = Regex.Match(queryLower, @"\b(last|past|previous)\s+(\d+)\s+days?\b");
        if (daysCountMatch.Success)
        {
            info.DaysCount = int.Parse(daysCountMatch.Groups[2].Value);
            info.EndDate = DateTime.Today;
            info.StartDate = DateTime.Today.AddDays(-info.DaysCount.Value);
        }

        // Extract time range: "5pm to 9pm", "17:00 to 21:00", "5:00pm-9:00pm"
        var timeRangeMatch = Regex.Match(query, @"(\d{1,2})(?::(\d{2}))?\s*(am|pm)?\s*(?:-|to)\s*(\d{1,2})(?::(\d{2}))?\s*(am|pm)?", RegexOptions.IgnoreCase);
        if (timeRangeMatch.Success)
        {
            int startHour = int.Parse(timeRangeMatch.Groups[1].Value);
            int endHour = int.Parse(timeRangeMatch.Groups[4].Value);
            string? startPeriod = timeRangeMatch.Groups[3].Value;
            string? endPeriod = timeRangeMatch.Groups[6].Value;

            // Convert to 24-hour format
            if (!string.IsNullOrEmpty(startPeriod) && startPeriod.ToLower() == "pm" && startHour < 12)
                startHour += 12;
            if (!string.IsNullOrEmpty(endPeriod) && endPeriod.ToLower() == "pm" && endHour < 12)
                endHour += 12;

            info.TimeBlockStart = $"{startHour:D2}:00";
            info.TimeBlockEnd = $"{endHour:D2}:00";
        }

        // Build date range from extracted info
        // If year is not specified but month/day/quarter are present, default to current year or most recent occurrence
        if (!info.Year.HasValue && (info.Month.HasValue || info.Day.HasValue || info.Quarter.HasValue))
        {
            // Default to 2025 for now (or could use DateTime.Now.Year)
            info.Year = 2025;
        }

        // Handle quarter extraction (Q1 = Jan-Mar, Q2 = Apr-Jun, Q3 = Jul-Sep, Q4 = Oct-Dec)
        if (info.Year.HasValue && info.Quarter.HasValue)
        {
            var startMonth = (info.Quarter.Value - 1) * 3 + 1; // Q1=1, Q2=4, Q3=7, Q4=10
            info.StartDate = new DateTime(info.Year.Value, startMonth, 1);
            info.EndDate = info.StartDate.Value.AddMonths(3);
        }
        else if (info.Year.HasValue && info.Month.HasValue && info.Day.HasValue)
        {
            try
            {
                info.StartDate = new DateTime(info.Year.Value, info.Month.Value, info.Day.Value);
                info.EndDate = info.StartDate.Value.AddDays(1);
            }
            catch { /* Invalid date */ }
        }
        else if (info.Year.HasValue && info.Month.HasValue)
        {
            try
            {
                info.StartDate = new DateTime(info.Year.Value, info.Month.Value, 1);
                info.EndDate = info.StartDate.Value.AddMonths(1);
            }
            catch { /* Invalid date */ }
        }
        else if (info.Year.HasValue)
        {
            info.StartDate = new DateTime(info.Year.Value, 1, 1);
            info.EndDate = new DateTime(info.Year.Value, 12, 31).AddDays(1);
        }

        return info;
    }

    /// <summary>
    /// Convert extracted date info to filter dictionary
    /// </summary>
    public static Dictionary<string, object> ToFiltersDictionary(ExtractedDateInfo dateInfo)
    {
        var filters = new Dictionary<string, object>();

        if (dateInfo.Year.HasValue)
            filters["year"] = dateInfo.Year.Value;

        if (dateInfo.Month.HasValue)
            filters["month"] = dateInfo.Month.Value;

        if (dateInfo.Day.HasValue)
            filters["day"] = dateInfo.Day.Value;

        if (dateInfo.StartDate.HasValue)
            filters["start_date"] = dateInfo.StartDate.Value.ToString("yyyy-MM-dd");

        if (dateInfo.EndDate.HasValue)
            filters["end_date"] = dateInfo.EndDate.Value.ToString("yyyy-MM-dd");

        if (!string.IsNullOrEmpty(dateInfo.TimeBlockStart))
            filters["time_block_start"] = dateInfo.TimeBlockStart;

        if (!string.IsNullOrEmpty(dateInfo.TimeBlockEnd))
            filters["time_block_end"] = dateInfo.TimeBlockEnd;

        return filters;
    }
}
