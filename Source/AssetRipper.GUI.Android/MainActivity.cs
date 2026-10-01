using Android.App;
using Android.Content;
using Android.OS;
using Android.Widget;
using Android.Views;

namespace AssetRipper.GUI.Android;

[Activity(Label = "AssetRipper", MainLauncher = true, Theme = "@android:style/Theme.Material.Light.NoActionBar")]
public class MainActivity : Activity
{
    private TextView? _logTextView;
    private ScrollView? _scrollView;
    private Button? _btnSelectFile;
    private Button? _btnStart;

    private string? _selectedInputPath;
    private string? _selectedOutputPath;
    private readonly AssetRipperService _ripperService = new();

    private const int REQUEST_PICK_FILE = 1001;

    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);

        // طلب صلاحيات الملفات لأجهزة أندرويد 11 و 12 و 13 (API 30+)
        if (OperatingSystem.IsAndroidVersionAtLeast(30))
        {
            if (!global::Android.OS.Environment.IsExternalStorageManager)
            {
                var intent = new Intent(global::Android.Provider.Settings.ActionManageAllFilesAccessPermission);
                StartActivity(intent);
            }
        }

        var layout = new LinearLayout(this)
        {
            Orientation = Orientation.Vertical,
            LayoutParameters = new ViewGroup.LayoutParams(ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.MatchParent)
        };
        layout.SetPadding(40, 60, 40, 30);

        var title = new TextView(this)
        {
            Text = "AssetRipper for Android",
            TextSize = 24,
            Gravity = GravityFlags.CenterHorizontal
        };
        layout.AddView(title);

        _btnSelectFile = new Button(this) { Text = "1. Select Game File (.apk / .bundle / .assets)" };
        _btnSelectFile.Click += (s, e) => OpenFilePicker();
        layout.AddView(_btnSelectFile);

        _btnStart = new Button(this) { Text = "2. Start Extraction" };
        _btnStart.Click += async (s, e) => await StartExtractionAsync();
        layout.AddView(_btnStart);

        _scrollView = new ScrollView(this)
        {
            LayoutParameters = new LinearLayout.LayoutParams(ViewGroup.LayoutParams.MatchParent, 0, 1.0f)
        };
        _logTextView = new TextView(this)
        {
            Text = "Ready. Logs will appear here...\n",
            TextSize = 12
        };
        _scrollView.AddView(_logTextView);
        layout.AddView(_scrollView);

        SetContentView(layout);

        _ripperService.OnLogReceived += (log) =>
        {
            RunOnUiThread(() =>
            {
                _logTextView.Append(log + "\n");
                _scrollView.FullScroll(FocusSearchDirection.Down);
            });
        };

        // حفظ الملفات المستخرجة في مجلد Download/AssetRipper_Export
        _selectedOutputPath = Path.Combine(
            global::Android.OS.Environment.GetExternalStoragePublicDirectory(global::Android.OS.Environment.DirectoryDownloads)!.AbsolutePath,
            "AssetRipper_Export"
        );
    }

    private void OpenFilePicker()
    {
        var intent = new Intent(Intent.ActionOpenDocument);
        intent.AddCategory(Intent.CategoryOpenable);
        intent.SetType("*/*");
        StartActivityForResult(intent, REQUEST_PICK_FILE);
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
            _btnSelectFile!.Text = "File Selected (Ready)";
            _logTextView!.Append($"Selected: {data.Data.Path}\n");
        }
    }

    private async Task StartExtractionAsync()
    {
        if (string.IsNullOrEmpty(_selectedInputPath))
        {
            Toast.MakeText(this, "Please select an asset/APK file first!", ToastLength.Short)!.Show();
            return;
        }

        Directory.CreateDirectory(_selectedOutputPath!);
        _btnStart!.Enabled = false;
        _logTextView!.Append($"Output Folder: {_selectedOutputPath}\n");
        _logTextView!.Append("Starting extraction...\n");

        await _ripperService.ExtractGameAsync(_selectedInputPath, _selectedOutputPath!);

        _btnStart!.Enabled = true;
        Toast.MakeText(this, "Extraction Process Finished!", ToastLength.Long)!.Show();
    }
}
