// Logic/DecisionEngine.cs
namespace DidiOverlay.Logic;

public enum Verdict { Accept, Reject }

public record RecommendationResult(
    Verdict Verdict,
    string Reason,
    double NetCOP,
    double RatePerMinCOP,
    double TotalKm
);

public class DecisionConfig
{
    // Umbrales básicos (ajustables en Configuración)
    public double FuelCostPerKmCOP { get; init; } = 500;
    public double MaxPickupKm      { get; init; } = 2.0;
    public int    MinNetCOP        { get; init; } = 3000;
    public int    MinRatePerMinCOP { get; init; } = 400;
    public double MinTripKm        { get; init; } = 1.0;
}

public static class DecisionEngine
{
    public static readonly DecisionConfig Defaults = new();

    public static RecommendationResult Evaluate(
        int fareCop, double pickupKm, double tripKm, int minutes, DecisionConfig? cfg = null)
    {
        cfg ??= Defaults;

        var totalKm = Math.Max(0.1, Math.Max(0, pickupKm) + Math.Max(0, tripKm));
        var cost    = totalKm * cfg.FuelCostPerKmCOP;
        var net     = fareCop - cost;
        var rpm     = minutes > 0 ? (double)fareCop / minutes : 0.0;

        bool okPickup = pickupKm <= cfg.MaxPickupKm;
        bool okNet    = net      >= cfg.MinNetCOP;
        bool okRPM    = rpm      >= cfg.MinRatePerMinCOP;
        bool okTrip   = tripKm   >= cfg.MinTripKm;

        if (okPickup && okNet && okRPM && okTrip)
        {
            var reason = $"Margen {net:0} COP · {rpm:0} COP/min · Recogida {pickupKm:0.0} km";
            return new RecommendationResult(Verdict.Accept, reason, net, rpm, totalKm);
        }

        // Motivo principal de rechazo
        string why;
        if (!okTrip)      why = $"Viaje corto ({tripKm:0.0} km)";
        else if (!okPickup) why = $"Recogida alta ({pickupKm:0.0} km)";
        else if (!okRPM)    why = $"COP/min bajo ({rpm:0})";
        else if (!okNet)    why = $"Margen bajo ({net:0} COP)";
        else                why = "Debajo de umbrales";

        return new RecommendationResult(Verdict.Reject, why, net, rpm, totalKm);
    }
}
