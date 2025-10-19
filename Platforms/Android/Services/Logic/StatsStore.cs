// Logic/StatsStore.cs
using Microsoft.Maui.Storage;

namespace DidiOverlay.Logic;

public record DailyStats(
    string DateIso,
    double TotalNetCOP,
    double TotalFareCOP,
    int TripsAccepted,
    int TripsRejected,
    int TripsConsidered
);

public static class StatsStore
{
    const string K_DATE  = "stats.date";
    const string K_NET   = "stats.total_net";
    const string K_FARE  = "stats.total_fare";
    const string K_A     = "stats.accepted";
    const string K_R     = "stats.rejected";
    const string K_C     = "stats.considered";

    static string TodayIso() => DateTime.Now.ToString("yyyy-MM-dd");

    public static DailyStats LoadToday()
    {
        var savedDate = Preferences.Default.Get(K_DATE, "");
        if (savedDate != TodayIso())
        {
            // si es otro día, reseteamos
            ResetToday();
        }
        return new DailyStats(
            Preferences.Default.Get(K_DATE, TodayIso()),
            Preferences.Default.Get(K_NET, 0.0),
            Preferences.Default.Get(K_FARE, 0.0),
            Preferences.Default.Get(K_A, 0),
            Preferences.Default.Get(K_R, 0),
            Preferences.Default.Get(K_C, 0)
        );
    }

    public static void ResetToday()
    {
        Preferences.Default.Set(K_DATE, TodayIso());
        Preferences.Default.Set(K_NET, 0.0);
        Preferences.Default.Set(K_FARE, 0.0);
        Preferences.Default.Set(K_A, 0);
        Preferences.Default.Set(K_R, 0);
        Preferences.Default.Set(K_C, 0);
    }

    static void Save(DailyStats s)
    {
        Preferences.Default.Set(K_DATE, s.DateIso);
        Preferences.Default.Set(K_NET,  s.TotalNetCOP);
        Preferences.Default.Set(K_FARE, s.TotalFareCOP);
        Preferences.Default.Set(K_A,    s.TripsAccepted);
        Preferences.Default.Set(K_R,    s.TripsRejected);
        Preferences.Default.Set(K_C,    s.TripsConsidered);
    }

    public static DailyStats AddAccepted(double netCop, double fareCop)
    {
        var s = LoadToday();
        s = s with
        {
            TotalNetCOP   = s.TotalNetCOP + netCop,
            TotalFareCOP  = s.TotalFareCOP + fareCop,
            TripsAccepted = s.TripsAccepted + 1
        };
        Save(s);
        return s;
    }

    public static DailyStats AddRejected()
    {
        var s = LoadToday();
        s = s with { TripsRejected = s.TripsRejected + 1 };
        Save(s);
        return s;
    }

    public static DailyStats AddConsidered()
    {
        var s = LoadToday();
        s = s with { TripsConsidered = s.TripsConsidered + 1 };
        Save(s);
        return s;
    }

    public static string ProgressLine(double goalCop, out double percent)
    {
        var s = LoadToday();
        percent = goalCop > 0 ? Math.Min(100.0, Math.Max(0.0, (s.TotalNetCOP / goalCop) * 100.0)) : 0.0;
        // números redondeados para overlay
        string p = percent.ToString("0");
        string net = s.TotalNetCOP.ToString("0");
        string goal = goalCop.ToString("0");
        return $"Meta: {net}/{goal} COP ({p}%) · Viajes: {s.TripsAccepted}/{s.TripsRejected} A/R";
    }
}
