using System;
using Microsoft.Maui.Controls;

#if ANDROID
using Android.App;
using Android.Content;
using Android.OS;
using Android.Provider;
using AndroidX.Core.App;
using AndroidX.Core.Content;
#endif

namespace DidiOverlay;

public partial class MainPage : ContentPage
{
    public MainPage()
    {
        InitializeComponent();
    }

#if ANDROID
    // =========================
    // BOTONES DE OCR
    // =========================

    // Abre el diálogo del sistema para permitir la captura de pantalla (MediaProjection)
    void OnPermitirCaptura(object sender, EventArgs e)
    {
        var ctx = Android.App.Application.Context;

        // Intenta resolver si la Activity quedó en Activities o en Services
        var starterType =
            Type.GetType("DidiOverlay.Platforms.Android.Activities.ScreenCaptureStarterActivity, DidiOverlay")
            ?? Type.GetType("DidiOverlay.Platforms.Android.Services.ScreenCaptureStarterActivity, DidiOverlay");

        if (starterType == null)
        {
            Microsoft.Maui.Controls.Application.Current?.MainPage?.DisplayAlert(
                "OCR",
                "No se encontró ScreenCaptureStarterActivity. Verifica el namespace (Activities o Services).",
                "OK");
            return;
        }

        // System.Type -> Java.Lang.Class para el ctor de Intent
        var starterJClass = Java.Lang.Class.FromType(starterType);
        if (starterJClass == null)
        {
            Microsoft.Maui.Controls.Application.Current?.MainPage?.DisplayAlert(
                "OCR",
                "No se pudo resolver la clase Java para ScreenCaptureStarterActivity.",
                "OK");
            return;
        }

        var intent = new Intent(ctx, starterJClass);
        intent.AddFlags(ActivityFlags.NewTask);
        ctx.StartActivity(intent);
    }

    // Dispara un OCR inmediato (si el servicio ya corre) o lo arranca como FGS
    void OnForzarOcr(object sender, EventArgs e)
    {
        var ctx = Android.App.Application.Context;

        var svcType = Type.GetType("DidiOverlay.Platforms.Android.Services.ScreenOcrService, DidiOverlay");
        if (svcType == null)
        {
            Microsoft.Maui.Controls.Application.Current?.MainPage?.DisplayAlert(
                "OCR",
                "No se encontró ScreenOcrService. Verifica el namespace.",
                "OK");
            return;
        }

        var svcJClass = Java.Lang.Class.FromType(svcType);
        if (svcJClass == null)
        {
            Microsoft.Maui.Controls.Application.Current?.MainPage?.DisplayAlert(
                "OCR",
                "No se pudo resolver la clase Java para ScreenOcrService.",
                "OK");
            return;
        }

        var svc = new Intent(ctx, svcJClass)
            .SetAction("com.didioverlay.action.FORCE_OCR"); // debe coincidir con ScreenOcrService.ActionForceOcr

        ContextCompat.StartForegroundService(ctx, svc);
    }

    // =========================
    // PERMISOS
    // =========================

    // Abre ajustes del permiso de superposición (SYSTEM_ALERT_WINDOW)
    void OnOpenOverlayPermission(object sender, EventArgs e)
    {
        try
        {
            var ctx = Android.App.Application.Context;
            var uri = Android.Net.Uri.Parse("package:" + ctx.PackageName);
            var intent = new Intent(Settings.ActionManageOverlayPermission, uri);
            intent.AddFlags(ActivityFlags.NewTask);
            ctx.StartActivity(intent);
        }
        catch (Exception ex)
        {
            Microsoft.Maui.Controls.Application.Current?.MainPage?.DisplayAlert("Overlay", $"Error: {ex.Message}", "OK");
        }
    }

    // Abre ajustes de Acceso a notificaciones (para el NotificationListenerService)
    void OnOpenNotificationAccess(object sender, EventArgs e)
    {
        try
        {
            var ctx = Android.App.Application.Context;
            var intent = new Intent(Settings.ActionNotificationListenerSettings);
            intent.AddFlags(ActivityFlags.NewTask);
            ctx.StartActivity(intent);
        }
        catch (Exception ex)
        {
            Microsoft.Maui.Controls.Application.Current?.MainPage?.DisplayAlert("Ajustes", $"Error: {ex.Message}", "OK");
        }
    }

    // =========================
    // LISTENER DE NOTIFICACIONES
    // =========================

    // Envía una notificación de prueba para verificar el listener
    void OnSendTestNotification(object sender, EventArgs e)
    {
        try
        {
            var ctx = Android.App.Application.Context;
            const string channelId = "didi_overlay_test";

            if (Build.VERSION.SdkInt >= BuildVersionCodes.O)
            {
                var nm = (NotificationManager)ctx.GetSystemService(Context.NotificationService);
                var ch = new NotificationChannel(channelId, "Pruebas DidiOverlay", NotificationImportance.Default)
                {
                    Description = "Canal para notificaciones de prueba"
                };
                nm.CreateNotificationChannel(ch);
            }

            var notif = new NotificationCompat.Builder(ctx, channelId)
                .SetContentTitle("DidiOverlay — prueba")
                .SetContentText("Mensaje de prueba para el listener de notificaciones.")
                .SetSmallIcon(Android.Resource.Drawable.IcDialogInfo)
                .SetAutoCancel(true)
                .Build();

            NotificationManagerCompat.From(ctx).Notify(1001, notif);
        }
        catch (Exception ex)
        {
            Microsoft.Maui.Controls.Application.Current?.MainPage?.DisplayAlert("Notificación", $"Error: {ex.Message}", "OK");
        }
    }

    // Verifica si el NotificationListenerService está habilitado
    void OnCheckListenerEnabled(object sender, EventArgs e)
    {
        try
        {
            var ctx = Android.App.Application.Context;
            var enabled = Settings.Secure.GetString(ctx.ContentResolver, "enabled_notification_listeners");

            var listenerJClass = Java.Lang.Class.FromType(typeof(DidiOverlay.Platforms.Android.Services.RideNotificationService));
            if (listenerJClass == null)
            {
                Microsoft.Maui.Controls.Application.Current?.MainPage?.DisplayAlert("Listener",
                    "No se pudo resolver la clase del listener.",
                    "OK");
                return;
            }

            var comp = new ComponentName(ctx, listenerJClass);

            bool isEnabled = enabled != null && comp != null && enabled.Contains(comp.FlattenToString());
            Microsoft.Maui.Controls.Application.Current?.MainPage?.DisplayAlert(
                "Listener de notificaciones",
                isEnabled ? "Habilitado ✅" : "Deshabilitado ❌",
                "OK");
        }
        catch (Exception ex)
        {
            Microsoft.Maui.Controls.Application.Current?.MainPage?.DisplayAlert("Listener", $"Error: {ex.Message}", "OK");
        }
    }
#endif
}
