using Android.App;
using Android.Content;
using Android.OS;
using Android.Webkit;
using Android.Views;

namespace AssetRipper.GUI.Android;

[Activity(Label = "AssetRipper", MainLauncher = true, Theme = "@android:style/Theme.Material.Light.NoActionBar")]
public class MainActivity : Activity
{
    private WebView? _webView;
    private readonly AssetRipperService _ripperService = new();
    private AssetRipperApiServer? _apiServer;
    private const int PORT = 5678;

    private const int REQUEST_PICK_FILE = 1001;
    private const int REQUEST_PICK_FOLDER = 1002;

    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);

        if (OperatingSystem.IsAndroidVersionAtLeast(30))
        {
            if (!global::Android.OS.Environment.IsExternalStorageManager)
            {
                var intent = new Intent(global::Android.Provider.Settings.ActionManageAllFilesAccessPermission);
                StartActivity(intent);
            }
        }

        // تشغيل خادم الـ REST API الرسمي لـ AssetRipper
        _apiServer = new AssetRipperApiServer(_ripperService, this, PORT);
        _apiServer.Start();

        var layout = new global::Android.Widget.LinearLayout(this)
        {
            Orientation = global::Android.Widget.Orientation.Vertical,
            LayoutParameters = new ViewGroup.LayoutParams(ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.MatchParent)
        };

        // زر علوي لفتح واجهة Swagger في متصفح Google Chrome
        var btnOpenChrome = new global::Android.Widget.Button(this)
        {
            Text = "🌐 Open Official Swagger in Chrome"
        };
        btnOpenChrome.Click += (s, e) =>
        {
            var intent = new Intent(Intent.ActionView, global::Android.Net.Uri.Parse($"http://127.0.0.1:{PORT}/swagger"));
            intent.AddFlags(ActivityFlags.NewTask);
            StartActivity(intent);
        };
        layout.AddView(btnOpenChrome);

        // شاشة الـ WebView الداخلية لعرض Swagger داخل التطبيق
        _webView = new WebView(this)
        {
            LayoutParameters = new global::Android.Widget.LinearLayout.LayoutParams(ViewGroup.LayoutParams.MatchParent, 0, 1.0f)
        };
        _webView.Settings.JavaScriptEnabled = true;
        _webView.Settings.DomStorageEnabled = true;
        _webView.SetWebViewClient(new WebViewClient());
        layout.AddView(_webView);

        SetContentView(layout);

        _webView.LoadUrl($"http://127.0.0.1:{PORT}/swagger");
    }

    public string GetDefaultExportPath()
    {
        string dir = Path.Combine(
            global::Android.OS.Environment.GetExternalStoragePublicDirectory(global::Android.OS.Environment.DirectoryDownloads)!.AbsolutePath,
            "AssetRipper_Export"
        );
        Directory.CreateDirectory(dir);
        return dir;
    }

    public void TriggerFilePicker()
    {
        var intent = new Intent(Intent.ActionOpenDocument);
        intent.AddCategory(Intent.CategoryOpenable);
        intent.SetType("*/*");
        StartActivityForResult(intent, REQUEST_PICK_FILE);
    }

    public void TriggerFolderPicker()
    {
        var intent = new Intent(Intent.ActionOpenDocumentTree);
        StartActivityForResult(intent, REQUEST_PICK_FOLDER);
    }

    protected override async void OnActivityResult(int requestCode, Result resultCode, Intent? data)
    {
        base.OnActivityResult(requestCode, resultCode, data);

        if (resultCode == Result.Ok && data?.Data != null)
        {
            if (requestCode == REQUEST_PICK_FILE)
            {
                string cachePath = Path.Combine(CacheDir!.AbsolutePath, "game_asset.bin");
                using var inputStream = ContentResolver!.OpenInputStream(data.Data);
                using var outputStream = System.IO.File.Create(cachePath);
                inputStream!.CopyTo(outputStream);

                await _ripperService.LoadGameAsync(new List<string> { cachePath });
                global::Android.Widget.Toast.MakeText(this, "Game File Loaded via API!", global::Android.Widget.ToastLength.Short)!.Show();
            }
            else if (requestCode == REQUEST_PICK_FOLDER)
            {
                string resolvedPath = ResolveStoragePath(data.Data);
                await _ripperService.LoadGameAsync(new List<string> { resolvedPath });
                global::Android.Widget.Toast.MakeText(this, $"PC Game Folder Mounted: {resolvedPath}", global::Android.Widget.ToastLength.Long)!.Show();
            }
        }
    }

    private string ResolveStoragePath(global::Android.Net.Uri uri)
    {
        string docId = global::Android.Provider.DocumentsContract.GetTreeDocumentId(uri) ?? "";
        string[] parts = docId.Split(':');
        string type = parts[0];
        string relativePath = parts.Length > 1 ? parts[1] : "";

        if ("primary".Equals(type, StringComparison.OrdinalIgnoreCase))
        {
            return Path.Combine(global::Android.OS.Environment.ExternalStorageDirectory!.AbsolutePath, relativePath);
        }
        return Path.Combine("/storage", type, relativePath);
    }
}
