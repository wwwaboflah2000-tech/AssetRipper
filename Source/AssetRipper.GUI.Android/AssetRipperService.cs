using AssetRipper.Export.UnityProjects;
using AssetRipper.Export.PrimaryContent;
using AssetRipper.Export.Configuration;
using AssetRipper.Import.Logging;
using AssetRipper.Import.Structure.GameStructure;
using AssetRipper.IO.Files;

namespace AssetRipper.GUI.Android;

public class AssetRipperService
{
    public event Action<string>? OnLogReceived;
    public string CurrentTarget { get; private set; } = "None";
    public bool IsLoaded { get; private set; } = false;

    private GameData? _loadedGameData;
    private readonly FullConfiguration _configuration = new();
    private readonly LocalFileSystem _fileSystem = new();

    public AssetRipperService()
    {
        Logger.Add(new CustomAndroidLogger(msg => OnLogReceived?.Invoke(msg)));
    }

    public async Task<bool> LoadGameAsync(List<string> paths)
    {
        return await Task.Run(() =>
        {
            try
            {
                OnLogReceived?.Invoke($"[AssetRipper API] Loading paths ({paths.Count} items)...");
                var exportHandler = new ExportHandler(_configuration);
                _loadedGameData = exportHandler.Load(paths, _fileSystem);

                OnLogReceived?.Invoke("[AssetRipper API] Processing game data...");
                exportHandler.Process(_loadedGameData);

                IsLoaded = true;
                CurrentTarget = string.Join(", ", paths);
                OnLogReceived?.Invoke("[AssetRipper API] Game loaded and processed successfully!");
                return true;
            }
            catch (Exception ex)
            {
                OnLogReceived?.Invoke($"[ERROR] Loading failed: {ex.Message}");
                IsLoaded = false;
                return false;
            }
        });
    }

    public async Task<bool> ExportUnityProjectAsync(string outputDir)
    {
        return await Task.Run(() =>
        {
            if (_loadedGameData == null)
            {
                OnLogReceived?.Invoke("[ERROR] No game loaded. Please execute LoadFile/LoadFolder first.");
                return false;
            }

            try
            {
                OnLogReceived?.Invoke($"[AssetRipper API] Exporting full Unity Project to: {outputDir}");
                var exportHandler = new ExportHandler(_configuration);
                exportHandler.Export(_loadedGameData, outputDir, _fileSystem);
                OnLogReceived?.Invoke("[AssetRipper API] Export Unity Project finished successfully!");
                return true;
            }
            catch (Exception ex)
            {
                OnLogReceived?.Invoke($"[ERROR] Export failed: {ex.Message}");
                return false;
            }
        });
    }

    public async Task<bool> ExportPrimaryContentAsync(string outputDir)
    {
        return await Task.Run(() =>
        {
            if (_loadedGameData == null)
            {
                OnLogReceived?.Invoke("[ERROR] No game loaded. Please execute LoadFile/LoadFolder first.");
                return false;
            }

            try
            {
                OnLogReceived?.Invoke($"[AssetRipper API] Exporting Raw Primary Content to: {outputDir}");
                var primaryExporter = new PrimaryContentExporter(_configuration);
                primaryExporter.Export(_loadedGameData.GameBundle, outputDir, _fileSystem);
                OnLogReceived?.Invoke("[AssetRipper API] Export Primary Content finished successfully!");
                return true;
            }
            catch (Exception ex)
            {
                OnLogReceived?.Invoke($"[ERROR] Export primary failed: {ex.Message}");
                return false;
            }
        });
    }

    public void Reset()
    {
        _loadedGameData = null;
        IsLoaded = false;
        CurrentTarget = "None";
        OnLogReceived?.Invoke("[AssetRipper API] Engine state reset.");
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
