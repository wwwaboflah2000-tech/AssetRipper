using Android.App;
using Android.Content;
using Android.OS;
using Android.Webkit;
using Android.Views;
using Java.Interop;

namespace AssetRipper.GUI.Android;

[Activity(Label = "AssetRipper", MainLauncher = true, Theme = "@android:style/Theme.Material.Light.NoActionBar")]
public class MainActivity : Activity
{
    private WebView? _webView;
    private string? _selectedInputPath;
    private string? _selectedOutputPath;
    private readonly AssetRipperService _ripperService = new();

    private const int REQUEST_PICK_FILE = 1001;

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

        // إنشاء متصفح الـ WebView المدمج
        _webView = new WebView(this)
        {
            LayoutParameters = new ViewGroup.LayoutParams(ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.MatchParent)
        };

        // تفعيل ميزات الويب المتقدمة واللمس وJavaScript
        _webView.Settings.JavaScriptEnabled = true;
        _webView.Settings.DomStorageEnabled = true;
        _webView.Settings.AllowFileAccess = true;
        _webView.Settings.AllowContentAccess = true;
        _webView.SetWebViewClient(new WebViewClient());

        // ربط جسر التواصل بين JS و C#
        _webView.AddJavascriptInterface(new WebAppInterface(this), "AndroidBridge");

        SetContentView(_webView);

        // تحميل الواجهة الرسمية المدمجة
        _webView.LoadUrl("file:///android_asset/web/index.html");

        _ripperService.OnLogReceived += (log) =>
        {
            RunOnUiThread(() =>
            {
                string safeLog = log.Replace("'", "\\'").Replace("\n", " ");
                _webView.EvaluateJavascript($"log('{safeLog}');", null);
            });
        };

        _selectedOutputPath = Path.Combine(
            global::Android.OS.Environment.GetExternalStoragePublicDirectory(global::Android.OS.Environment.DirectoryDownloads)!.AbsolutePath,
            "AssetRipper_Export"
        );
    }

    public void TriggerFilePicker()
    {
        var intent = new Intent(Intent.ActionOpenDocument);
        intent.AddCategory(Intent.CategoryOpenable);
        intent.SetType("*/*");
        StartActivityForResult(intent, REQUEST_PICK_FILE);
    }

    public async void StartExtraction(bool exportAsUnity)
    {
        if (string.IsNullOrEmpty(_selectedInputPath)) return;

        Directory.CreateDirectory(_selectedOutputPath!);
        await _ripperService.ExtractGameAsync(_selectedInputPath, _selectedOutputPath!);

        RunOnUiThread(() =>
        {
            _webView?.EvaluateJavascript("onExtractionCompleted(true);", null);
        });
    }

    protected override void OnActivityResult(int requestCode, Result resultCode, Intent? data)
    {
        base.OnActivityResult(requestCode, resultCode, data);

        if (resultCode == Result.Ok && data?.Data != null)
        {
            string cachePath = Path.Combine(CacheDir!.AbsolutePath, "input_game_asset.bin");
            using var inputStream = ContentResolver!.OpenInputStream(data.Data);
            using var outputStream = System.IO.File.Create(cachePath);
            inputStream!.CopyTo(outputStream);

            _selectedInputPath = cachePath;
            string fileName = System.IO.Path.GetFileName(data.Data.Path ?? "game_asset");

            RunOnUiThread(() =>
            {
                _webView?.EvaluateJavascript($"onFileSelected('{fileName}');", null);
            });
        }
    }

    // كلاس جسر التواصل بين واجهة الويب ونظام أندرويد
    public class WebAppInterface : Java.Lang.Object
    {
        private readonly MainActivity _activity;
        public WebAppInterface(MainActivity activity) => _activity = activity;

        [Export]
        [JavascriptInterface]
        public void pickFile()
        {
            _activity.RunOnUiThread(() => _activity.TriggerFilePicker());
        }

        [Export]
        [JavascriptInterface]
        public void runExtraction(bool isUnityProject)
        {
            _activity.StartExtraction(isUnityProject);
        }
    }
}
