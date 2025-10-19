#if ANDROID
// Platforms/Android/Services/RideNotificationService.cs
using System;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using Android.App;
using Android.Content;
using Android.OS;
using Android.Service.Notification;
using Android.Util;
using AWidget = global::Android.Widget; // Toast

namespace DidiOverlay.Platforms.Android.Services;

[global::Android.Runtime.Preserve(AllMembers = true)]
[Service(
    Name = "com.didioverlay.app.RideNotificationService",
    Label = "Ride Notification Listener",
    Permission = "android.permission.BIND_NOTIFICATION_LISTENER_SERVICE",
    Exported = true
)]
[IntentFilter(new[] { "android.service.notification.NotificationListenerService" })]
public class RideNotificationService : NotificationListenerService
{
    const string TAG = "DidiOverlayNotif";
    const string ACTION_OFFER = "com.didioverlay.ACTION_OFFER";

    // ✅ SOLO DiDi Conductor (ajusta si tu región usa otro paquete)
    static readonly string[] AllowedPackages = new[]
    {
        "com.didiglobal.driver",
        "com.xiaojukeji.driver",
        "com.sdu.didi.psdriver"
    };

    // Frases típicas de RUÍDO (estado/promos) -> ignorar
    static readonly string[] NoiseContains = new[]
    {
        "estás conectado", "estas conectado", // acentos
        "espera una solicitud de viaje",
        "has dejado de recibir arrendamientos",
        "haz clic para esperar arrendamientos",
        "tienes un mensaje nuevo",
        "completa arrendamientos",
        "multiplica tus ganancias",
        "promoción", "promo", "recompensa", "bono", "bonificación",
        "didi moto", "informa", "tips", "consejos",
    };

    // Indicadores de que PODRÍA ser una OFERTA (al menos uno)
    static readonly string[] OfferHints = new[]
    {
        "nueva solicitud", "nueva orden", "nueva oferta",
        "solicitud de viaje", "pedido", "viaje",
        "recogida", "pickup"
    };

    // Evita procesar la misma notificación repetida
    readonly ConcurrentDictionary<int, long> _seen = new();

    public override void OnListenerConnected()
    {
        base.OnListenerConnected();
        Log.Info(TAG, "NotificationListener conectado.");
        try { AWidget.Toast.MakeText(this, "Listener de notificaciones activo", AWidget.ToastLength.Short).Show(); } catch { }
    }

    public override void OnListenerDisconnected()
    {
        base.OnListenerDisconnected();
        Log.Warn(TAG, "NotificationListener desconectado.");
    }

    public override void OnNotificationPosted(StatusBarNotification? sbn)
    {
        try
        {
            if (sbn == null) return;

            var pkg = sbn.PackageName ?? "";
            if (!AllowedPackages.Contains(pkg))
                return;

            if (_seen.TryGetValue(sbn.Id, out var t) && sbn.PostTime == t) return;
            _seen[sbn.Id] = sbn.PostTime;

            var body = CollectAllText(sbn);

            // Filtro de ruido
            if (LooksLikeNoise(body))
            {
                Log.Info(TAG, $"PKG={pkg} · IGNORADO (ruido) · Texto='{TrimLong(body, 220)}'");
                return;
            }

            Log.Info(TAG, $"PKG={pkg} · Texto='{TrimLong(body, 220)}'");

            // ¿Parece oferta?
            if (!LooksLikeOffer(body))
            {
                Log.Info(TAG, "No parece oferta (a la espera de una solicitud real).");
                return;
            }

            var offer = ParseOfferFromText(body);
            if (offer == null)
            {
                Log.Warn(TAG, "Parece oferta pero faltan datos (COP/min/km). Mantendremos el log para afinar.");
                return;
            }

            Log.Info(TAG, $"Oferta: fare={offer.FareCop} COP, min={offer.Minutes}, pickup={offer.PickupKm:0.##} km, trip={offer.TripKm:0.##} km");

            var intent = new Intent(ACTION_OFFER);
            intent.PutExtra("fareCop", offer.FareCop);
            intent.PutExtra("minutes", offer.Minutes);
            intent.PutExtra("pickupKm", (float)offer.PickupKm);
            intent.PutExtra("tripKm", (float)offer.TripKm);
            SendBroadcast(intent);
        }
        catch (Exception ex)
        {
            Log.Error(TAG, $"OnNotificationPosted error: {ex}");
        }
    }

    // ---- Heurísticas ----

    static bool LooksLikeNoise(string body)
    {
        foreach (var k in NoiseContains)
            if (body.Contains(k, StringComparison.OrdinalIgnoreCase))
                return true;
        return false;
    }

    static bool LooksLikeOffer(string body)
    {
        // Si menciona conceptos de viaje/pedido Y trae números típicos de oferta (COP/min/km)
        bool hasHint = OfferHints.Any(h => body.Contains(h, StringComparison.OrdinalIgnoreCase));
        bool hasCop  = Regex.IsMatch(body, @"(?:cop|\$)\s*[0-9\.\,]{4,}") || Regex.IsMatch(body, @"(^|\D)\d{5,}(\D|$)");
        bool hasMin  = Regex.IsMatch(body, @"\b\d+\s*(?:min|mins|minutos)\b");
        bool hasKm   = Regex.IsMatch(body, @"\d+(?:[.,]\d+)?\s*km");

        return hasHint && (hasCop || hasMin || hasKm);
    }

    // ---- Parseo ----

    class Offer
    {
        public int    FareCop;
        public int    Minutes;
        public double PickupKm;
        public double TripKm;
    }

    static Offer? ParseOfferFromText(string body)
    {
        var fare = TryParseCOP(body);
        var mins = TryParseInt(body, @"(\d+)\s*(?:min|mins|minutos)");

        var kmMatches = Regex.Matches(body, @"(\d+(?:[.,]\d+)?)\s*km");
        var kms = kmMatches.Cast<Match>()
                           .Select(m => ParseDoubleNorm(m.Groups[1].Value))
                           .Where(v => v > 0).ToList();

        double pickup = 0, trip = 0;
        if (kms.Count >= 2)
        {
            // Heurística: el menor suele ser la recogida, el mayor el trayecto
            pickup = kms.Min();
            trip   = kms.Max();
        }
        else if (kms.Count == 1)
        {
            var pickNear = Regex.Match(body, @"recogida[^\d]{0,12}(\d+(?:[.,]\d+)?)\s*km");
            if (pickNear.Success) pickup = ParseDoubleNorm(pickNear.Groups[1].Value);
            else trip = kms[0];
        }

        // Relaja condiciones: aceptamos si hay AL MENOS minutos o km o COP
        if (fare <= 0 && mins <= 0 && pickup <= 0 && trip <= 0)
            return null;

        return new Offer { FareCop = fare, Minutes = mins, PickupKm = pickup, TripKm = trip };
    }

    // ---- Helpers parse ----

    static int TryParseCOP(string s)
    {
        // $138.200 , $138200 , COP 138.200 , 138200
        var m = Regex.Match(s, @"(?:cop|\$)\s*([0-9\.\,]{4,})|(^|\D)(\d{5,})(\D|$)");
        if (m.Success)
        {
            var raw  = m.Groups[1].Success ? m.Groups[1].Value : m.Groups[3].Value;
            var norm = raw.Replace(".", "").Replace(",", "").Trim();
            if (int.TryParse(norm, out var v)) return v;
        }
        return 0;
    }

    static int TryParseInt(string s, string pattern)
    {
        var m = Regex.Match(s, pattern);
        if (m.Success && int.TryParse(m.Groups[1].Value, out var v)) return v;
        return 0;
    }

    static double ParseDoubleNorm(string raw)
    {
        var norm = raw.Replace(",", ".").Trim();
        return double.TryParse(norm, NumberStyles.Any, CultureInfo.InvariantCulture, out var v) ? v : 0;
    }

    // ---- Recolección de texto ----

    static string CollectAllText(StatusBarNotification sbn)
    {
        var n  = sbn.Notification;
        var ex = n?.Extras;

        string safe(object? o) => (o?.ToString() ?? "").Trim();

        var title  = safe(ex?.GetString(Notification.ExtraTitle));
        var text   = safe(ex?.GetString(Notification.ExtraText));
        var big    = safe(ex?.GetCharSequence(Notification.ExtraBigText));
        var sub    = safe(ex?.GetString(Notification.ExtraSubText));
        var info   = safe(ex?.GetString(Notification.ExtraInfoText));
        var ticker = safe(n?.TickerText);

        var sb = new StringBuilder();
        sb.Append(title).Append(' ')
          .Append(text).Append(' ')
          .Append(big).Append(' ')
          .Append(sub).Append(' ')
          .Append(info).Append(' ')
          .Append(ticker);

        try
        {
            var lines = ex?.GetCharSequenceArray(Notification.ExtraTextLines);
            if (lines != null && lines.Length > 0)
                foreach (var l in lines) sb.Append(' ').Append(safe(l));
        }
        catch { /* algunas ROMs fallan aquí; ignorar */ }

        return sb.ToString().Replace('\n', ' ').ToLowerInvariant();
    }

    static string TrimLong(string s, int max)
        => (s.Length <= max) ? s : (s.Substring(0, max) + "…");
}
#endif
