using AssetRipper.Export.UnityProjects;
using AssetRipper.Export.Configuration;
using AssetRipper.Import.Logging;
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
                
                // 1. استخدام FullConfiguration المطلوب للنسخة الحديثة
                var configuration = new FullConfiguration();

                // 2. إنشاء الكائن واستدعاء Load عبر الكائن نفسه (exportHandler)
                var exportHandler = new ExportHandler(configuration);
                var gameData = exportHandler.Load(paths, fileSystem);
                
                OnLogReceived?.Invoke("[AssetRipper] Game loaded successfully. Processing assets...");

                // 3. معالجة البيانات
                exportHandler.Process(gameData);
                OnLogReceived?.Invoke($"[AssetRipper] Processing finished. Exporting to: {outputDirectory}...");

                // 4. تصدير الأصول
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
