using System.Globalization;

namespace Playnite.Avalonia.App.Services;

public class DateFormattingOptions : SettingsValueObject
{
    private string format = "d";
    private bool pastWeekRelativeFormat;

    public string Format { get => format; set => SetField(ref format, value); }
    public bool PastWeekRelativeFormat { get => pastWeekRelativeFormat; set => SetField(ref pastWeekRelativeFormat, value); }

    public DateFormattingOptions Clone() => new()
    {
        Format = Format,
        PastWeekRelativeFormat = PastWeekRelativeFormat
    };
}

public sealed class ReleaseDateFormattingOptions : DateFormattingOptions
{
    private string partialFormat = "y";

    public string PartialFormat { get => partialFormat; set => SetField(ref partialFormat, value); }

    public new ReleaseDateFormattingOptions Clone() => new()
    {
        Format = Format,
        PastWeekRelativeFormat = PastWeekRelativeFormat,
        PartialFormat = PartialFormat
    };
}

public static class DateFormattingService
{
    public const string DefaultFormat = "d";
    public const string DefaultPartialFormat = "y";

    public static string Format(
        DateTime date,
        DateFormattingOptions options,
        DateTime? today = null,
        CultureInfo culture = null)
    {
        culture ??= CultureInfo.CurrentCulture;
        options ??= new DateFormattingOptions();
        if (options.PastWeekRelativeFormat)
        {
            var dayDifference = ((today ?? DateTime.Today).Date - date.Date).TotalDays;
            if (dayDifference == 0)
            {
                return "Today";
            }

            if (dayDifference == 1)
            {
                return "Yesterday";
            }

            if (dayDifference is > 1 and < 7)
            {
                return date.ToString("dddd", culture);
            }
        }

        return date.ToString(NormalizeFormat(options.Format, DefaultFormat), culture);
    }

    public static string FormatReleaseDate(
        DateTime date,
        bool hasMonth,
        bool hasDay,
        ReleaseDateFormattingOptions options,
        DateTime? today = null,
        CultureInfo culture = null)
    {
        options ??= new ReleaseDateFormattingOptions();
        if (!hasMonth)
        {
            return date.Year.ToString(culture ?? CultureInfo.CurrentCulture);
        }

        if (!hasDay)
        {
            return date.ToString(NormalizeFormat(options.PartialFormat, DefaultPartialFormat),
                culture ?? CultureInfo.CurrentCulture);
        }

        return Format(date, options, today, culture);
    }

    public static bool IsValidFormat(string format)
    {
        if (string.IsNullOrWhiteSpace(format))
        {
            return false;
        }

        try
        {
            _ = new DateTime(2001, 2, 3, 16, 5, 6).ToString(format, CultureInfo.InvariantCulture);
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
    }

    public static string NormalizeFormat(string format, string fallback) =>
        IsValidFormat(format) ? format : fallback;
}
