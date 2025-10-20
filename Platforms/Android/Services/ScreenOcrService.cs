#if ANDROID
//#define HAS_MLKIT  // <- Actívalo cuando instales los NuGet de ML Kit

using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

using global::Android.App;
using global::Android.Content;
using global::Android.Graphics;
using global::Android.Hardware.Display;                  // VirtualDisplay, VirtualDisplayFlags
using global::Android.Media;
using global::Android.Media.Projection;
using global::Android.OS;
using global::Android.Util;
using AndroidX.Core.App;

namespace DidiOverlay.Platforms.Android.Services;

[Service(
    Name = "com.didioverlay.app.ScreenOcrService",
    Exported = false,
    ForegroundServiceType = global::Android.Content.PM.ForegroundService.TypeMediaProjection
)]
public class ScreenOcrService : Service
{
    const string TAG = "DidiOverlayOCR";
    const string CH_ID = "ocr_channel";
    const int NOTIF_ID = 2042;
    const string ACTION_OFFER = "com.didioverlay.ACTION_OFFER";

    MediaProjection? _proj;
    VirtualDisplay? _vdisp;
    ImageReader? _reader;
    CancellationTokenSource? _cts;

#if HAS_MLKIT
    // Cuando ACTIVES ML Kit, mueve estos using a la cabecera del archivo:
    // using Google.MLKit.Vision.Common;
    // using Google.MLKit.Vision.Text;
    // ITextRecognizer? _recognizer;
#endif

    long _lastOcrMs = 0;

    public override void OnCreate()
    {
        base.OnCreate();
        CreateChannel();
        var notif = new NotificationCompat.Builder(this, CH_ID)
            .SetContentTitle("OCR de pantalla activo")
            .SetContentText("Reconociendo ofertas en pantalla…")
            .SetSmallIcon(global::Android.Resource.Drawable.IcDialogInfo)
            .SetOngoing(true)
            .Build();
        StartForeground(NOTIF_ID, notif);
    }

    void CreateChannel()
    {
        if (Build.VERSION.SdkInt >= BuildVersionCodes.O)
        {
            var nm = (NotificationManager)GetSystemService(NotificationService)!;
            var ch = new NotificationChannel(CH_ID, "OCR", NotificationImportance.Low);
            nm.CreateNotificationChannel(ch);
        }
    }

    public override StartCommandResult OnStartCommand(Intent? intent, StartCommandFlags flags, int startId)
    {
        try
        {
            int resultCode = intent?.GetIntExtra("mp.resultCode", (int)global::Android.App.Result.Canceled) ?? (int)global::Android.App.Result.Canceled;
            var data = (Intent?)intent?.GetParcelableExtra("mp.data");
            var mgr = (MediaProjectionManager)GetSystemService(MediaProjectionService)!;
            _proj = mgr.GetMediaProjection(resultCode, data!);
            if (_proj == null) { StopSelf(); return StartCommandResult.NotSticky; }

            var metrics = Resources!.DisplayMetrics!;
            int width  = metrics.WidthPixels;
            int height = metrics.HeightPixels;
            int dpi    = (int)metrics.DensityDpi;

            // ✅ Formato: forzamos RGBA_8888 casteando desde Android.Graphics.Format
            _reader = ImageReader.NewInstance(
                width,
                height,
                (global::Android.Graphics.ImageFormatType)global::Android.Graphics.Format.Rgba8888,
                2
            );

            // ✅ Flags neutros (algunas bindings piden Android.Views.DisplayFlags)
            _vdisp = _proj.CreateVirtualDisplay(
                "didi_overlay_vd",
                width, height, dpi,
                (global::Android.Views.DisplayFlags)0,
                _reader.Surface,
                null,
                null
            );

            _reader.SetOnImageAvailableListener(new ImageListener(this, width, height), null);

#if HAS_MLKIT
            // _recognizer = Google.MLKit.Vision.Text.TextRecognition.GetClient(
            //     new Google.MLKit.Vision.Text.TextRecognizerOptions.Builder().Build()
            // );
#endif
            _cts = new CancellationTokenSource();

            Log.Info(TAG, $"MediaProjection iniciada ({width}x{height}@{dpi}).");
        }
        catch (Exception ex)
        {
            Log.Error(TAG, $"OnStartCommand: {ex}");
            StopSelf();
        }
        return StartCommandResult.Sticky;
    }

    public override void OnDestroy()
    {
        try { _cts?.Cancel(); } catch { }
        try { _vdisp?.Release(); } catch { }
        try { _reader?.Close(); } catch { }
        try { _proj?.Stop(); } catch { }
        base.OnDestroy();
    }

    public override IBinder? OnBind(Intent? intent) => null;

    async Task ProcessBitmapAsync(Bitmap bmp)
    {
        var now = Java.Lang.JavaSystem.CurrentTimeMillis();
        if (now - _lastOcrMs < 700) { bmp.Recycle(); return; }
        _lastOcrMs = now;

        try
        {
#if HAS_MLKIT
            var img = Google.MLKit.Vision.Common.InputImage.FromBitmap(bmp, 0);
            var res = await _recognizer!.Process(img);

            string text = (res?.Text ?? "").ToLowerInvariant().Replace("\n", " ");
            if (string.IsNullOrWhiteSpace(text)) { TrySaveDebug(bmp, ""); return; }

            TrySaveDebug(bmp, text);

            int fare = TryParseCOP(text);
            int mins = TryParseInt(text, @"(\d+)\s*(?:min|mins|minutos)");
            var kms = Regex.Matches(text, @"(\d+(?:[.,]\d+)?)\s*km")
                           .Cast<Match>()
                           .Select(m => ParseDoubleNorm(m.Groups[1].Value))
                           .Where(v => v > 0).ToList();
            double pickup = 0, trip = 0;
            if (kms.Count >= 2) { pickup = kms.Min(); trip = kms.Max(); }
            else if (kms.Count == 1)
            {
                var pickNear = Regex.Match(text, @"recogida[^\d]{0,12}(\d+(?:[.,]\d+)?)\s*km");
                if (pickNear.Success) pickup = ParseDoubleNorm(m.Groups[1].Value);
                else trip = kms[0];
            }

            if (fare <= 0 && mins <= 0 && pickup <= 0 && trip <= 0) return;

            var intent = new Intent(ACTION_OFFER);
            intent.PutExtra("fareCop", fare);
            intent.PutExtra("minutes", mins);
            intent.PutExtra("pickupKm", (float)pickup);
            intent.PutExtra("tripKm", (float)trip);
            SendBroadcast(intent);
            Log.Info(TAG, $"OCR Oferta => COP={fare}, min={mins}, pick={pickup:0.##}, trip={trip:0.##}");
#else
            // Sin ML Kit: guarda un frame para diagnosticar FLAG_SECURE / contenido
            TrySaveDebug(bmp, "");
#endif
        }
        catch (Exception ex)
        {
            Log.Warn(TAG, $"OCR error: {ex.Message}");
        }
        finally
        {
            try { bmp.Recycle(); } catch { }
        }
    }

    void TrySaveDebug(Bitmap bmp, string text)
    {
        try
        {
            var dir = GetExternalFilesDir(null)!.AbsolutePath;
            if (!File.Exists(System.IO.Path.Combine(dir, "ocr_once.flag")))
            {
                File.WriteAllText(System.IO.Path.Combine(dir, "ocr_once.flag"), "1");
                using var fs = File.OpenWrite(System.IO.Path.Combine(dir, $"frame_{DateTime.Now:HHmmss}.png"));
                bmp.Compress(Bitmap.CompressFormat.Png, 100, fs);
                File.WriteAllText(System.IO.Path.Combine(dir, $"text_{DateTime.Now:HHmmss}.txt"), text ?? "");
                Log.Info(TAG, $"Debug guardado en {dir}");
            }
        }
        catch { }
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
        return double.TryParse(norm, System.Globalization.NumberStyles.Any,
            System.Globalization.CultureInfo.InvariantCulture, out var v) ? v : 0;
    }

    class ImageListener : Java.Lang.Object, ImageReader.IOnImageAvailableListener
    {
        readonly ScreenOcrService _svc;
        readonly int _w, _h;
        public ImageListener(ScreenOcrService svc, int w, int h) { _svc = svc; _w = w; _h = h; }

        public async void OnImageAvailable(ImageReader? reader)
        {
            if (reader == null || _svc._cts?.IsCancellationRequested == true) return;

            using var img = reader.AcquireLatestImage();
            if (img == null) return;

            try
            {
                var plane = img.GetPlanes()[0];
                var buf = plane.Buffer;
                var pixelStride = plane.PixelStride;
                var rowStride   = plane.RowStride;
                var rowPadding  = rowStride - pixelStride * _w;

                var bmp = Bitmap.CreateBitmap(_w + rowPadding / pixelStride, _h, Bitmap.Config.Argb8888);
                bmp.CopyPixelsFromBuffer(buf);

                var cropped = Bitmap.CreateBitmap(bmp, 0, 0, _w, _h);
                bmp.Recycle();

                await _svc.ProcessBitmapAsync(cropped);
            }
            catch (Exception ex)
            {
                Log.Warn(TAG, $"OnImageAvailable: {ex.Message}");
            }
            finally
            {
                img.Close();
            }
        }
    }
}
#endif
