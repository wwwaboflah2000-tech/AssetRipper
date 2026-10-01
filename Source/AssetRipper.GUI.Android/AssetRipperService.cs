using AssetRipper.Export.UnityProjects;
using AssetRipper.Import.Logging;
using AssetRipper.Import.Configuration;
using AssetRipper.IO.Files;

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
                var fileSystem = new LocalFileSystem();
                var configuration = new CoreConfiguration();

                // 1. إنشاء كائن الـ ExportHandler وتمرير الـ FileSystem
                var exportHandler = new ExportHandler(configuration);
                var gameData = ExportHandler.Load(paths, fileSystem);
                
                OnLogReceived?.Invoke("[AssetRipper] Game loaded successfully. Processing assets...");

                // 2. معالجة البيانات عبر كائن exportHandler
                exportHandler.Process(gameData);
                OnLogReceived?.Invoke($"[AssetRipper] Processing finished. Exporting to: {outputDirectory}...");

                // 3. تصدير الأصول مع تمرير الـ FileSystem
                exportHandler.Export(gameData, outputDirectory, fileSystem);
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
