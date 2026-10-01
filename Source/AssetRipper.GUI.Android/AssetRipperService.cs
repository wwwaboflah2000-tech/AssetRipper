using AssetRipper.Export.UnityProjects;
using AssetRipper.Import.Logging;

namespace AssetRipper.GUI.Android;

public class AssetRipperService
{
    public event Action<string>? OnLogReceived;

    public AssetRipperService()
    {
        Logger.Add(new CustomAndroidLogger(msg => OnLogReceived?.Invoke(msg)));
    }

    public async Task ExtractGameAsync(string inputFilePath, string outputDirectory)
    {
        await Task.Run(() =>
        {
            try
            {
                OnLogReceived?.Invoke($"[AssetRipper] Loading: {inputFilePath}");
                var paths = new List<string> { inputFilePath };
                
                // تحميل ملفات وبنى اللعبة
                var gameData = ExportHandler.Load(paths);
                OnLogReceived?.Invoke("[AssetRipper] Game loaded successfully. Processing assets...");

                // معالجة الأصول (فك الهياكل، الأنيميشن، وغيرها)
                ExportHandler.Process(gameData);
                OnLogReceived?.Invoke($"[AssetRipper] Processing finished. Exporting to: {outputDirectory}...");

                // تصدير الأصول
                ExportHandler.Export(gameData, outputDirectory);
                OnLogReceived?.Invoke("[AssetRipper] Completed successfully!");
            }
            catch (Exception ex)
            {
                OnLogReceived?.Invoke($"[ERROR] Extraction failed: {ex.Message}\n{ex.StackTrace}");
            }
        });
    }

    private class CustomAndroidLogger : ILogger
    {
        private readonly Action<string> _logCallback;
        public CustomAndroidLogger(Action<string> logCallback) => _logCallback = logCallback;

        public void Log(LogType type, LogCategory category, string message)
        {
            _logCallback?.Invoke($"[{type}] {message}");
        }

        public void BlankLine(int count = 1) { }
    }
}
