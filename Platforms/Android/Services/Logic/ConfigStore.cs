// Logic/ConfigStore.cs
using Microsoft.Maui.Storage;

namespace DidiOverlay.Logic;

public static class ConfigStore
{
    // Claves de configuración
    const string K_FUEL     = "cfg.fuel_per_km";
    const string K_PICK     = "cfg.max_pickup_km";
    const string K_MINN     = "cfg.min_net_cop";
    const string K_RPM      = "cfg.min_rate_per_min";
    const string K_MINTRIP  = "cfg.min_trip_km";

    const string K_GOAL     = "cfg.daily_goal_cop";
    const string K_VIBRATE  = "cfg.alert_vibrate";
    const string K_SOUND    = "cfg.alert_sound";
    const string K_AUTOHIDE = "cfg.autohide_secs";

    // Guarda TODO (umbrales + meta + alertas + auto-ocultar)
    public static void Save(DecisionConfig cfg, int dailyGoalCop, bool alertVibrate, bool alertSound, int autoHideSecs)
    {
        Preferences.Default.Set(K_FUEL,    cfg.FuelCostPerKmCOP);
        Preferences.Default.Set(K_PICK,    cfg.MaxPickupKm);
        Preferences.Default.Set(K_MINN,    cfg.MinNetCOP);
        Preferences.Default.Set(K_RPM,     cfg.MinRatePerMinCOP);
        Preferences.Default.Set(K_MINTRIP, cfg.MinTripKm);

        Preferences.Default.Set(K_GOAL,    dailyGoalCop);
        Preferences.Default.Set(K_VIBRATE, alertVibrate);
        Preferences.Default.Set(K_SOUND,   alertSound);
        Preferences.Default.Set(K_AUTOHIDE,autoHideSecs);
    }

    // Carga solo los umbrales de decisión
    public static DecisionConfig Load() => new DecisionConfig
    {
        FuelCostPerKmCOP = Preferences.Default.Get(K_FUEL,    new DecisionConfig().FuelCostPerKmCOP),
        MaxPickupKm      = Preferences.Default.Get(K_PICK,    new DecisionConfig().MaxPickupKm),
        MinNetCOP        = Preferences.Default.Get(K_MINN,    new DecisionConfig().MinNetCOP),
        MinRatePerMinCOP = Preferences.Default.Get(K_RPM,     new DecisionConfig().MinRatePerMinCOP),
        MinTripKm        = Preferences.Default.Get(K_MINTRIP, new DecisionConfig().MinTripKm),
    };

    // Lecturas puntuales usadas por el OverlayService
    public static int  LoadDailyGoalCOP(int fallback = 120000) => Preferences.Default.Get(K_GOAL, fallback);
    public static bool LoadAlertVibrate(bool fallback = true)  => Preferences.Default.Get(K_VIBRATE, fallback);
    public static bool LoadAlertSound(bool fallback = false)   => Preferences.Default.Get(K_SOUND,   fallback);
    public static int  LoadAutoHideSeconds(int fallback = 8)   => Preferences.Default.Get(K_AUTOHIDE,fallback);
}
