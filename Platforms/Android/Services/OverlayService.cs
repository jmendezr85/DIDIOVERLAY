#if ANDROID
using System;
using global::Android.App;
using global::Android.Content;
using global::Android.Content.PM;   // <-- AQUÍ
using global::Android.OS;
using global::Android.Runtime; // JavaCast
using AndroidX.Core.App;
using AViews = global::Android.Views;
using AWidget = global::Android.Widget;
using Gfx = global::Android.Graphics;
using Drw = global::Android.Graphics.Drawables;
using DidiOverlay.Logic;
using Microsoft.Maui.Storage; // Preferences
using AMedia = global::Android.Media;

namespace DidiOverlay.Platforms.Android.Services;

[Service(
    Name = "com.didioverlay.app.OverlayService",
    Exported = false,
    ForegroundServiceType = ForegroundService.TypeDataSync   // <-- YA RESUELVE
)]
public class OverlayService : Service
{
    const int NOTIF_ID = 1010;
    const string CHANNEL_ID   = "overlay_channel";
    const string K_UI_COMPACT = "ui.compact";
    const string ACTION_OFFER = "com.didioverlay.ACTION_OFFER";
    const string ACTION_TOGGLE_COMPACT = "com.didioverlay.ACTION_TOGGLE_COMPACT";
    const string ACTION_STOP = "com.didioverlay.ACTION_STOP";
    const string ACTION_OPEN_OVERLAY_SETTINGS = "com.didioverlay.ACTION_OPEN_OVERLAY_SETTINGS";

    AViews.WindowManagerLayoutParams? _params;
    AViews.IWindowManager? _wm;
    AWidget.LinearLayout? _rootLayout; bool _viewAdded = false;

    AWidget.TextView? _titleView;
    AWidget.TextView? _bodyView;
    AWidget.TextView? _progressView;
    AWidget.Button? _btnAccept;
    AWidget.Button? _btnReject;
    AWidget.Button? _btnCompact;
    AWidget.LinearLayout? _buttonsRow;
    AWidget.LinearLayout? _header;

    Drw.GradientDrawable? _bgDrawable;

    BroadcastReceiver? _offerReceiver;
    BroadcastReceiver? _actionReceiver;

    int _lastFare = 0, _lastMinutes = 0;
    double _lastPickup = 0, _lastTrip = 0, _lastNet = 0;
    Verdict _lastVerdict = Verdict.Reject;
    bool _compact = false;

    Handler? _hideHandler;
    Java.Lang.IRunnable? _hideRunnable;

    readonly Handler _main = new Handler(Looper.MainLooper!);

    public override void OnCreate()
    {
        base.OnCreate();
        _compact = Preferences.Default.Get(K_UI_COMPACT, false);
        _hideHandler = new Handler(Looper.MainLooper!);

        CreateChannel();
        StartInForeground("Esperando ofertas…");

        RunOnUiThread(() =>
        {
            if (!EnsureOverlayPermission())
            {
                UpdateForegroundNotification("Permiso de superposición requerido");
                return;
            }

            EnsureWindowManager();
            ShowOverlayCard();
            RegisterReceivers();
            UpdateProgressLine();
            ApplyCompact();
        });
    }

    public override StartCommandResult OnStartCommand(Intent? intent, StartCommandFlags flags, int startId)
        => StartCommandResult.Sticky;

    public override void OnDestroy()
    {
        RunOnUiThread(() =>
        {
            try
            {
                CancelAutoHide();
                if (_offerReceiver != null) UnregisterReceiver(_offerReceiver);
                if (_actionReceiver != null) UnregisterReceiver(_actionReceiver);
                if (_rootLayout != null && _wm != null && _viewAdded)
                {
                    _wm.RemoveView(_rootLayout);
                    _viewAdded = false;
                }
            }
            catch { }
            _offerReceiver = null;
            _actionReceiver = null;
            _rootLayout = null;
        });

        base.OnDestroy();
    }

    public override IBinder? OnBind(Intent? intent) => null;

    // ---------- Foreground notif ----------
    void CreateChannel()
    {
        if (Build.VERSION.SdkInt >= BuildVersionCodes.O)
        {
            var ch = new NotificationChannel(CHANNEL_ID, "Overlay", NotificationImportance.Min)
            {
                Description = "DidiOverlay servicio en primer plano"
            };
            ch.EnableLights(false);
            ch.EnableVibration(false);
            ch.SetShowBadge(false);
            var nm = (NotificationManager)GetSystemService(NotificationService)!;
            nm.CreateNotificationChannel(ch);
        }
    }

    void StartInForeground(string text) => StartForeground(NOTIF_ID, BuildOngoingNotification(text));

    void UpdateForegroundNotification(string text)
        => NotificationManagerCompat.From(this).Notify(NOTIF_ID, BuildOngoingNotification(text));

    Notification BuildOngoingNotification(string contentText)
    {
        var builder = new NotificationCompat.Builder(this, CHANNEL_ID)
            .SetSmallIcon(global::Android.Resource.Drawable.IcDialogInfo)
            .SetContentTitle("DidiOverlay activo")
            .SetContentText(contentText)
            .SetOngoing(true)
            .SetOnlyAlertOnce(true)
            .SetPriority((int)NotificationPriority.Min);

        var togglePi = PendingIntent.GetBroadcast(
            this, 1, new Intent(ACTION_TOGGLE_COMPACT).SetPackage(PackageName),
            PendingIntentFlags.UpdateCurrent | (Build.VERSION.SdkInt >= BuildVersionCodes.M ? PendingIntentFlags.Immutable : 0)
        );
        builder.AddAction(new NotificationCompat.Action.Builder(0, _compact ? "Expandir" : "Compactar", togglePi).Build());

        var stopPi = PendingIntent.GetBroadcast(
            this, 2, new Intent(ACTION_STOP).SetPackage(PackageName),
            PendingIntentFlags.UpdateCurrent | (Build.VERSION.SdkInt >= BuildVersionCodes.M ? PendingIntentFlags.Immutable : 0)
        );
        builder.AddAction(new NotificationCompat.Action.Builder(0, "Cerrar", stopPi).Build());

        if (!global::Android.Provider.Settings.CanDrawOverlays(this))
        {
            var permPi = PendingIntent.GetBroadcast(
                this, 3, new Intent(ACTION_OPEN_OVERLAY_SETTINGS).SetPackage(PackageName),
                PendingIntentFlags.UpdateCurrent | (Build.VERSION.SdkInt >= BuildVersionCodes.M ? PendingIntentFlags.Immutable : 0)
            );
            builder.AddAction(new NotificationCompat.Action.Builder(0, "Permiso overlay", permPi).Build());
        }

        return builder.Build();
    }

    // ---------- Overlay ----------
    bool EnsureOverlayPermission()
    {
        if (Build.VERSION.SdkInt >= BuildVersionCodes.M &&
            !global::Android.Provider.Settings.CanDrawOverlays(this))
        {
            AWidget.Toast.MakeText(this, "Falta permiso de superposición", AWidget.ToastLength.Long).Show();
            return false;
        }
        return true;
    }

    void EnsureWindowManager()
    {
        if (_wm != null) return;
        var wmObj = GetSystemService(WindowService) ?? throw new Exception("WindowService es null");
        _wm = wmObj.JavaCast<AViews.IWindowManager>() ?? throw new Exception("No se pudo obtener IWindowManager");
    }

    void ShowOverlayCard()
    {
        var type = Build.VERSION.SdkInt >= BuildVersionCodes.O
            ? AViews.WindowManagerTypes.ApplicationOverlay
            : AViews.WindowManagerTypes.Phone;

        _params = new AViews.WindowManagerLayoutParams(
            AViews.WindowManagerLayoutParams.WrapContent,
            AViews.WindowManagerLayoutParams.WrapContent,
            type,
            AViews.WindowManagerFlags.NotFocusable |
            AViews.WindowManagerFlags.NotTouchModal |
            AViews.WindowManagerFlags.LayoutInScreen |
            AViews.WindowManagerFlags.LayoutNoLimits,
            Gfx.Format.Translucent)
        {
            Gravity = AViews.GravityFlags.Top | AViews.GravityFlags.End,
            X = Dp(12),
            Y = Dp(56)
        };

        _rootLayout = new AWidget.LinearLayout(this) { Orientation = AWidget.Orientation.Vertical };
        int pad = Dp(12);
        _rootLayout.SetPadding(pad, pad, pad, pad);
        _rootLayout.SetBackgroundDrawable(CreateCardBackground(Gfx.Color.Rgb(27, 94, 32)));
        if (Build.VERSION.SdkInt >= BuildVersionCodes.Lollipop) _rootLayout.Elevation = Dp(8);

        _header = new AWidget.LinearLayout(this) { Orientation = AWidget.Orientation.Horizontal };

        _titleView = new AWidget.TextView(this)
        {
            Text = "Overlay listo ✅",
            TextSize = 16,
            Ellipsize = global::Android.Text.TextUtils.TruncateAt.End
        };
        _titleView.SetSingleLine(true);
        _titleView.SetTextColor(Gfx.Color.White);
        _titleView.Typeface = Gfx.Typeface.DefaultBold;

        _btnCompact = new AWidget.Button(this) { Text = "≡" };
        _btnCompact.SetPadding(Dp(8), 0, Dp(8), 0);
        _btnCompact.Click += (s, e) =>
        {
            _compact = !_compact;
            Preferences.Default.Set(K_UI_COMPACT, _compact);
            ApplyCompact();
            UpdateForegroundNotification(_compact ? "Modo compacto" : "Esperando ofertas…");
        };

        var titleLp = new AWidget.LinearLayout.LayoutParams(0, AViews.ViewGroup.LayoutParams.WrapContent, 1f);
        _header.AddView(_titleView, titleLp);
        _header.AddView(_btnCompact);

        _bodyView = new AWidget.TextView(this) { Text = "Esperando ofertas…", TextSize = 14 };
        _bodyView.SetTextColor(Gfx.Color.White);

        _progressView = new AWidget.TextView(this) { Text = "Meta: 0/0 COP (0%) · Viajes: 0/0 A/R", TextSize = 12 };
        _progressView.SetTextColor(Gfx.Color.Argb(220, 230, 230, 230));

        _buttonsRow = new AWidget.LinearLayout(this) { Orientation = AWidget.Orientation.Horizontal };
        _buttonsRow.SetPadding(0, Dp(6), 0, 0);

        _btnAccept = new AWidget.Button(this) { Text = "✓ Acepté" };
        _btnReject = new AWidget.Button(this) { Text = "✕ Rechacé" };

        _btnAccept.Click += (s, e) => { StatsStore.AddAccepted(_lastNet, _lastFare); AWidget.Toast.MakeText(this, "Registrado como ACEPTADO", AWidget.ToastLength.Short).Show(); UpdateProgressLine(); };
        _btnReject.Click += (s, e) => { StatsStore.AddRejected(); AWidget.Toast.MakeText(this, "Registrado como RECHAZADO", AWidget.ToastLength.Short).Show(); UpdateProgressLine(); };

        _buttonsRow.AddView(_btnAccept);
        _buttonsRow.AddView(_btnReject);

        _rootLayout.AddView(_header);
        _rootLayout.AddView(_bodyView);
        _rootLayout.AddView(_progressView);
        _rootLayout.AddView(_buttonsRow);

        _header.SetOnTouchListener(new DragTouchListener(() => _wm!, () => _params!, () => _rootLayout!));

        if (!_viewAdded) { _wm!.AddView(_rootLayout, _params); _viewAdded = true; }
    }

    Drw.GradientDrawable CreateCardBackground(Gfx.Color color)
    {
        var d = new Drw.GradientDrawable();
        d.SetShape(Drw.ShapeType.Rectangle);
        d.SetColor(color);
        d.SetCornerRadius(Dp(12));
        d.SetStroke(Dp(1), Gfx.Color.Argb(60, 255, 255, 255));
        _bgDrawable = d;
        return d;
    }

    void UpdateCardColor(Gfx.Color color)
    {
        if (_bgDrawable == null) { var bg = CreateCardBackground(color); _rootLayout?.SetBackgroundDrawable(bg); }
        else { _bgDrawable.SetColor(color); _rootLayout?.Invalidate(); }
    }

    void ApplyCompact()
    {
        if (_bodyView == null || _progressView == null || _buttonsRow == null || _rootLayout == null) return;
        _bodyView.Visibility     = _compact ? AViews.ViewStates.Gone : AViews.ViewStates.Visible;
        _progressView.Visibility = _compact ? AViews.ViewStates.Gone : AViews.ViewStates.Visible;
        _buttonsRow.Visibility   = _compact ? AViews.ViewStates.Gone : AViews.ViewStates.Visible;

        int pad = _compact ? Dp(8) : Dp(12);
        _rootLayout.SetPadding(pad, pad, pad, pad);
    }

    void EnsureVisible()
    {
        if (_rootLayout == null) return;
        _rootLayout.Visibility = AViews.ViewStates.Visible;
        _rootLayout.Alpha = 1f;
    }

    int Dp(int dp) => (int)((Resources?.DisplayMetrics?.Density ?? 1f) * dp);

    void SetOverlay(string title, string body, Verdict v)
    {
        RunOnUiThread(() =>
        {
            if (_titleView != null) _titleView.Text = title;
            if (_bodyView  != null) _bodyView.Text  = body;

            var color = v == Verdict.Accept ? Gfx.Color.Rgb(46, 125, 50) : Gfx.Color.Rgb(183, 28, 28);
            UpdateCardColor(color);
        });
    }

    void UpdateProgressLine()
    {
        var goal = ConfigStore.LoadDailyGoalCOP();
        var line = StatsStore.ProgressLine(goal, out var _);
        RunOnUiThread(() => { if (_progressView != null) _progressView.Text = line; });
    }

    void TriggerAlerts(Verdict v)
    {
        if (v != Verdict.Accept) return;

        if (ConfigStore.LoadAlertVibrate(true))
        {
            try
            {
                global::Android.OS.Vibrator? vib = null;
                if (Build.VERSION.SdkInt >= BuildVersionCodes.S)
                    vib = ((global::Android.OS.VibratorManager?)GetSystemService(VibratorManagerService))?.DefaultVibrator;
                else
                    vib = (global::Android.OS.Vibrator?)GetSystemService(VibratorService);

                if (vib != null && vib.HasVibrator)
                {
                    if (Build.VERSION.SdkInt >= BuildVersionCodes.O)
                        vib.Vibrate(global::Android.OS.VibrationEffect.CreateOneShot(120, global::Android.OS.VibrationEffect.DefaultAmplitude));
                    else
#pragma warning disable CA1422
                        vib.Vibrate(120);
#pragma warning restore CA1422
                }
            } catch { }
        }

        if (ConfigStore.LoadAlertSound(false))
        {
            try
            {
                var uri = AMedia.RingtoneManager.GetDefaultUri(AMedia.RingtoneType.Notification);
                var ring = AMedia.RingtoneManager.GetRingtone(this, uri);
                ring?.Play();
            } catch { }
        }
    }

    void CancelAutoHide()
    {
        if (_hideRunnable != null) _hideHandler?.RemoveCallbacks(_hideRunnable);
        _hideRunnable = null;
    }

    void ScheduleAutoHide()
    {
        var secs = ConfigStore.LoadAutoHideSeconds(8);
        if (secs <= 0) { CancelAutoHide(); return; }

        CancelAutoHide();
        _hideRunnable = new Java.Lang.Runnable(() =>
        {
            try { if (_rootLayout != null) _rootLayout.Visibility = AViews.ViewStates.Invisible; } catch { }
        });
        _hideHandler?.PostDelayed(_hideRunnable, secs * 1000);
    }

    void RegisterReceivers()
    {
        try { if (_offerReceiver != null) UnregisterReceiver(_offerReceiver); } catch { }
        _offerReceiver = new OfferReceiver(UpdateFromIntent);
        RegisterReceiver(_offerReceiver, new IntentFilter(ACTION_OFFER));

        try { if (_actionReceiver != null) UnregisterReceiver(_actionReceiver); } catch { }
        _actionReceiver = new ActionReceiver(this);
        var f = new IntentFilter();
        f.AddAction(ACTION_TOGGLE_COMPACT);
        f.AddAction(ACTION_STOP);
        f.AddAction(ACTION_OPEN_OVERLAY_SETTINGS);
        RegisterReceiver(_actionReceiver, f);
    }

    void UpdateFromIntent(Intent intent)
    {
        if (intent == null || intent.Action != ACTION_OFFER) return;

        int ReadInt(string key, int def = 0)
        {
            try { return intent.GetIntExtra(key, def); } catch { }
            var s = intent.GetStringExtra(key);
            return int.TryParse((s ?? "").Trim(), out var v) ? v : def;
        }
        double ReadDouble(string key, double def = 0)
        {
            try { var d = intent.GetDoubleExtra(key, double.NaN); if (!double.IsNaN(d)) return d; } catch {}
            try { var f = intent.GetFloatExtra (key, float.NaN);  if (!float.IsNaN(f))  return f; } catch {}
            var s = intent.GetStringExtra(key);
            return (s != null && double.TryParse(s.Replace(",", "."),
                System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out var v)) ? v : def;
        }

        _lastFare    = ReadInt("fareCop", 0);
        _lastPickup  = ReadDouble("pickupKm", 0);
        _lastTrip    = ReadDouble("tripKm", 0);
        _lastMinutes = ReadInt("minutes", 0);

        EnsureVisible();

        var cfg = ConfigStore.Load();
        var res = DecisionEngine.Evaluate(_lastFare, _lastPickup, _lastTrip, _lastMinutes, cfg);

        _lastVerdict = res.Verdict;
        _lastNet     = res.NetCOP;

        string title = res.Verdict == Verdict.Accept ? "✅ Aceptar" : "⛔ Rechazar";
        var body = _compact ? $"{res.Reason}"
                            : $"{res.Reason}\nTotalKm {res.TotalKm:0.0} · Margen {res.NetCOP:0} · {res.RatePerMinCOP:0} COP/min";

        SetOverlay(title, body, res.Verdict);
        UpdateProgressLine();

        TriggerAlerts(res.Verdict);
        ScheduleAutoHide();

        UpdateForegroundNotification(res.Verdict == Verdict.Accept ? "Oferta recomendable" : "Oferta no recomendable");
    }

    void RunOnUiThread(Action a)
    {
        if (Looper.MyLooper() == Looper.MainLooper) a();
        else _main.Post(a);
    }

    class DragTouchListener : Java.Lang.Object, AViews.View.IOnTouchListener
    {
        readonly Func<AViews.IWindowManager> _wm;
        readonly Func<AViews.WindowManagerLayoutParams> _lp;
        readonly Func<AViews.View> _root;
        int _startX, _startY; float _touchX, _touchY;

        public DragTouchListener(Func<AViews.IWindowManager> wm, Func<AViews.WindowManagerLayoutParams> lp, Func<AViews.View> root)
        { _wm = wm; _lp = lp; _root = root; }

        public bool OnTouch(AViews.View? v, AViews.MotionEvent? e)
        {
            if (v == null || e == null) return false;
            switch (e.Action)
            {
                case AViews.MotionEventActions.Down:
                    _startX = _lp().X; _startY = _lp().Y;
                    _touchX = e.RawX;  _touchY = e.RawY;
                    return true;
                case AViews.MotionEventActions.Move:
                    int dx = (int)(e.RawX - _touchX);
                    int dy = (int)(e.RawY - _touchY);
                    _lp().X = _startX - dx;   // ancla derecha
                    _lp().Y = _startY + dy;   // ancla arriba
                    _wm().UpdateViewLayout(_root(), _lp());
                    return true;
            }
            return false;
        }
    }

    class OfferReceiver : BroadcastReceiver
    {
        private readonly Action<Intent> _onReceive;
        public OfferReceiver(Action<Intent> onReceive) => _onReceive = onReceive;
        public override void OnReceive(Context? context, Intent? intent)
        {
            if (intent == null) return;
            _onReceive(intent);
        }
    }

    class ActionReceiver : BroadcastReceiver
    {
        readonly OverlayService _svc;
        public ActionReceiver(OverlayService svc) => _svc = svc;

        public override void OnReceive(Context? context, Intent? intent)
        {
            if (intent == null) return;
            var act = intent.Action ?? string.Empty;

            switch (act)
            {
                case ACTION_TOGGLE_COMPACT:
                    _svc._compact = !_svc._compact;
                    Preferences.Default.Set(K_UI_COMPACT, _svc._compact);
                    _svc.ApplyCompact();
                    _svc.UpdateForegroundNotification(_svc._compact ? "Modo compacto" : "Esperando ofertas…");
                    break;
                case ACTION_STOP:
                    _svc.StopSelf();
                    break;
                case ACTION_OPEN_OVERLAY_SETTINGS:
                    try
                    {
                        var i = new Intent(global::Android.Provider.Settings.ActionManageOverlayPermission);
                        i.SetFlags(ActivityFlags.NewTask);
                        _svc.StartActivity(i);
                    } catch { }
                    break;
            }
        }
    }
}
#endif
