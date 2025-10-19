#if ANDROID
// Platforms/Android/Services/OrderAccessibilityService.cs
using System;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Globalization;

// global:: para no resolver como DidiOverlay.Platforms.Android
using global::Android.App;
using global::Android.Content;
using global::Android.OS;
using global::Android.Util;
using global::Android.Views.Accessibility; // EventTypes, AccessibilityNodeInfo, AccessibilityEvent, AccessibilityWindowInfo

namespace DidiOverlay.Platforms.Android.Services;

[Service(
    Name = "com.didioverlay.app.OrderAccessibilityService",
    Permission = "android.permission.BIND_ACCESSIBILITY_SERVICE",
    Exported = true
)]
[IntentFilter(new[] { "android.accessibilityservice.AccessibilityService" })]
[MetaData("android.accessibilityservice", Resource = "@xml/order_accessibility_config")]
public class OrderAccessibilityService : global::Android.AccessibilityServices.AccessibilityService
{
    const string TAG = "DidiOverlayA11y";
    const string ACTION_OFFER = "com.didioverlay.ACTION_OFFER";

    // Deja esto en true mientras afinamos
    const bool DEBUG_DUMP_TREE = true;

    static readonly string[] DriverPkgs = {
        "com.didiglobal.driver", "com.xiaojukeji.driver", "com.sdu.didi.psdriver"
    };

    // Pistas típicas de oferta
    static readonly string[] OfferHints = {
        "nueva solicitud", "solicitud de viaje", "nueva orden", "pedido",
        "aceptar", "rechazar", "recogida", "pickup", "destino", "viaje",
        "min", "minutos", "km", "kilómetros", "kilometros", "cop", "$"
    };

    Handler? _pollHandler;
    Java.Lang.IRunnable? _pollRunnable;

    protected override void OnServiceConnected()
    {
        base.OnServiceConnected();
        _pollHandler = new Handler(Looper.MainLooper!);
        Log.Info(TAG, "AccessibilityService conectado.");
    }

    public override void OnAccessibilityEvent(AccessibilityEvent? e)
    {
        try
        {
            if (e == null) return;
            var pkg = e.PackageName?.ToString() ?? "";
            if (!DriverPkgs.Contains(pkg)) return;

            if (e.EventType is EventTypes.WindowStateChanged
                              or EventTypes.WindowContentChanged
                              or EventTypes.NotificationStateChanged
                              or EventTypes.ViewTextChanged
                              or EventTypes.ViewTextSelectionChanged)
            {
                ProcessCurrentUiTree("event");
                SchedulePolling(); // por si la oferta aparece milisegundos después
            }
        }
        catch (Exception ex)
        {
            Log.Error(TAG, $"OnAccessibilityEvent error: {ex}");
        }
    }

    public override void OnInterrupt() { }

    // --------- Polling (re-lectura durante ~4s) ----------
    void SchedulePolling()
    {
        CancelPolling();
        if (_pollHandler == null) return;

        int ticks = 0;
        _pollRunnable = new Java.Lang.Runnable(() =>
        {
            try
            {
                ticks++;
                ProcessCurrentUiTree($"poll#{ticks}");
                if (ticks < 6 && _pollHandler != null && _pollRunnable != null)
                    _pollHandler.PostDelayed(_pollRunnable, 700);
            }
            catch { }
        });
        _pollHandler.PostDelayed(_pollRunnable, 350);
    }

    void CancelPolling()
    {
        if (_pollRunnable != null && _pollHandler != null)
            _pollHandler.RemoveCallbacks(_pollRunnable);
        _pollRunnable = null;
    }

    // --------- Lectura de la UI (TODAS las ventanas) ----------
    void ProcessCurrentUiTree(string reason)
    {
        var (text, dump) = CollectAllWindowsTextAndDump();
        if (string.IsNullOrWhiteSpace(text))
        {
            Log.Debug(TAG, $"A11y {reason}: texto vacío.");
            return;
        }

        bool looksLikeOffer = OfferHints.Any(h => text.Contains(h, StringComparison.OrdinalIgnoreCase));

        if (DEBUG_DUMP_TREE && looksLikeOffer && !string.IsNullOrEmpty(dump))
            Log.Debug(TAG, $"A11y dump ({reason}):\n{dump}");

        var offer = ParseOffer(text);
        if (offer != null)
        {
            Log.Info(TAG, $"A11y Oferta: fare={offer.FareCop} COP, min={offer.Minutes}, pick={offer.PickupKm:0.##}km, trip={offer.TripKm:0.##}km");
            var intent = new Intent(ACTION_OFFER);
            intent.PutExtra("fareCop", offer.FareCop);
            intent.PutExtra("minutes", offer.Minutes);
            intent.PutExtra("pickupKm", (float)offer.PickupKm);
            intent.PutExtra("tripKm", (float)offer.TripKm);
            SendBroadcast(intent);
        }
        else
        {
            if (looksLikeOffer)
                Log.Debug(TAG, $"A11y {reason}: texto (parece oferta pero sin parseo): '{TrimLong(text, 500)}'");
            else
                Log.Debug(TAG, $"A11y {reason}: texto (sin oferta): '{TrimLong(text, 250)}'");
        }
    }

    (string text, string dump) CollectAllWindowsTextAndDump()
    {
        var flat = new StringBuilder();
        var dump = new StringBuilder();

        // 1) Ventana activa
        var root = RootInActiveWindow;
        if (root != null)
        {
            try { Walk(root, flat, dump, 0, 0); }
            finally { root.Recycle(); }
        }

        // 2) Otras ventanas (diálogos, overlays)
        try
        {
            var wins = Windows; // IList<AccessibilityWindowInfo>
            if (wins != null)
            {
                for (int w = 0; w < wins.Count; w++)
                {
                    var win = wins[w];
                    try
                    {
                        var r = win?.Root;
                        if (r != null)
                        {
                            Walk(r, flat, dump, 0, w + 1);
                            r.Recycle();
                        }
                    }
                    catch { }
                    finally { win?.Recycle(); }
                }
            }
        }
        catch { /* algunas ROMs dan null o excepciones aquí */ }

        var s = flat.ToString().Replace("\n", " ").Replace("\r", " ").ToLowerInvariant();
        s = Regex.Replace(s, @"\s+", " ").Trim();
        var d = dump.ToString();
        if (d.Length > 8000) d = d.Substring(0, 8000) + "…";
        return (s, d);
    }

    void Walk(AccessibilityNodeInfo node, StringBuilder flat, StringBuilder dump, int depth, int index)
    {
        try
        {
            var txt  = node.Text?.ToString();
            var desc = node.ContentDescription?.ToString();
            var viewId = node.ViewIdResourceName ?? "";
            var cls    = node.ClassName ?? "";
            var pkg    = node.PackageName ?? "";

            string indent = new string(' ', Math.Min(depth, 16) * 2);
            if (DEBUG_DUMP_TREE)
            {
                dump.Append(indent)
                    .Append($"[{depth}:{index}] {cls} id='{viewId}' pkg='{pkg}' ")
                    .Append($"txt='{(txt ?? "").Replace("\n"," ")}' ")
                    .Append($"desc='{(desc ?? "").Replace("\n"," ")}'")
                    .AppendLine();
            }

            var t = (txt ?? desc ?? "").Trim();
            if (!string.IsNullOrEmpty(t))
                flat.Append(t).Append(' ');

            int childCount = node.ChildCount;
            for (int i = 0; i < childCount; i++)
            {
                var c = node.GetChild(i);
                if (c != null) { Walk(c, flat, dump, depth + 1, i); c.Recycle(); }
            }
        }
        catch { }
    }

    // --------- Parseo ---------
    class Offer
    {
        public int    FareCop;
        public int    Minutes;
        public double PickupKm;
        public double TripKm;
    }

    Offer? ParseOffer(string body)
    {
        var fare = TryParseCOP(body);
        var mins = TryParseInt(body, @"(\d+)\s*(?:min|mins|minutos)");

        var kmMatches = Regex.Matches(body, @"(\d+(?:[.,]\d+)?)\s*km");
        var kms = kmMatches.Cast<Match>()
                           .Select(m => ParseDoubleNorm(m.Groups[1].Value))
                           .Where(v => v > 0).ToList();

        double pickup = 0, trip = 0;
        if (kms.Count >= 2) { pickup = kms.Min(); trip = kms.Max(); }
        else if (kms.Count == 1)
        {
            var pickNear = Regex.Match(body, @"recogida[^\d]{0,12}(\d+(?:[.,]\d+)?)\s*km");
            if (pickNear.Success) pickup = ParseDoubleNorm(pickNear.Groups[1].Value);
            else trip = kms[0];
        }

        if (fare <= 0 && mins <= 0 && pickup <= 0 && trip <= 0) return null;
        return new Offer { FareCop = fare, Minutes = mins, PickupKm = pickup, TripKm = trip };
    }

    static int TryParseCOP(string s)
    {
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

    static string TrimLong(string s, int max)
        => (s.Length <= max) ? s : (s.Substring(0, max) + "…");
}
#endif
