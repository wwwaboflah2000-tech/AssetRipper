using System.Net;
using System.Text;
using System.Text.Json;

namespace AssetRipper.GUI.Android;

public class AssetRipperApiServer
{
    private readonly HttpListener _listener = new();
    private readonly AssetRipperService _service;
    private readonly MainActivity _mainActivity;
    private readonly int _port;
    private readonly List<string> _recentLogs = new();

    public AssetRipperApiServer(AssetRipperService service, MainActivity activity, int port = 5678)
    {
        _service = service;
        _mainActivity = activity;
        _port = port;

        _service.OnLogReceived += log =>
        {
            lock (_recentLogs)
            {
                if (_recentLogs.Count > 100) _recentLogs.RemoveAt(0);
                _recentLogs.Add(log);
            }
        };
    }

    public void Start()
    {
        Task.Run(() =>
        {
            try
            {
                _listener.Prefixes.Add($"http://127.0.0.1:{_port}/");
                _listener.Start();

                while (_listener.IsListening)
                {
                    var context = _listener.GetContext();
                    Task.Run(() => HandleRequest(context));
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"API Server exception: {ex.Message}");
            }
        });
    }

    private async Task HandleRequest(HttpListenerContext context)
    {
        var req = context.Request;
        var res = context.Response;
        string path = req.Url?.AbsolutePath ?? "/";
        string method = req.HttpMethod.ToUpperInvariant();

        res.Headers.Add("Access-Control-Allow-Origin", "*");
        res.Headers.Add("Access-Control-Allow-Methods", "GET, POST, OPTIONS");
        res.Headers.Add("Access-Control-Allow-Headers", "Content-Type");

        if (method == "OPTIONS")
        {
            res.StatusCode = 204;
            res.Close();
            return;
        }

        try
        {
            // 1. Swagger UI Endpoints
            if (path == "/" || path == "/swagger" || path == "/swagger/index.html")
            {
                byte[] html = Encoding.UTF8.GetBytes(GetSwaggerHtml());
                res.ContentType = "text/html; charset=utf-8";
                res.ContentLength64 = html.Length;
                await res.OutputStream.WriteAsync(html);
            }
            else if (path == "/swagger/v1/swagger.json")
            {
                byte[] json = Encoding.UTF8.GetBytes(GetOpenApiSpec());
                res.ContentType = "application/json; charset=utf-8";
                res.ContentLength64 = json.Length;
                await res.OutputStream.WriteAsync(json);
            }
            // 2. Official AssetRipper Command Endpoints
            else if (path == "/Commands/LoadFile" && method == "POST")
            {
                _mainActivity.RunOnUiThread(() => _mainActivity.TriggerFilePicker());
                await SendJson(res, new { message = "Opening Android File Picker on device screen..." });
            }
            else if (path == "/Commands/LoadFolder" && method == "POST")
            {
                _mainActivity.RunOnUiThread(() => _mainActivity.TriggerFolderPicker());
                await SendJson(res, new { message = "Opening Android Folder Picker for PC Game..." });
            }
            else if (path == "/Commands/ExportUnityProject" && method == "POST")
            {
                string outDir = _mainActivity.GetDefaultExportPath();
                bool ok = await _service.ExportUnityProjectAsync(outDir);
                await SendJson(res, new { success = ok, exportPath = outDir });
            }
            else if (path == "/Commands/ExportPrimaryContent" && method == "POST")
            {
                string outDir = _mainActivity.GetDefaultExportPath();
                bool ok = await _service.ExportPrimaryContentAsync(outDir);
                await SendJson(res, new { success = ok, exportPath = outDir });
            }
            else if (path == "/Commands/Reset" && method == "POST")
            {
                _service.Reset();
                await SendJson(res, new { message = "Engine reset successfully." });
            }
            else if (path == "/api/status" && method == "GET")
            {
                List<string> logsCopy;
                lock (_recentLogs) { logsCopy = new List<string>(_recentLogs); }
                await SendJson(res, new {
                    isLoaded = _service.IsLoaded,
                    target = _service.CurrentTarget,
                    logs = logsCopy
                });
            }
            else
            {
                res.StatusCode = 404;
            }
        }
        catch (Exception ex)
        {
            res.StatusCode = 500;
            byte[] err = Encoding.UTF8.GetBytes(ex.Message);
            await res.OutputStream.WriteAsync(err);
        }
        finally
        {
            res.OutputStream.Close();
        }
    }

    private static async Task SendJson(HttpListenerResponse res, object data)
    {
        byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(data);
        res.ContentType = "application/json; charset=utf-8";
        res.ContentLength64 = bytes.Length;
        await res.OutputStream.WriteAsync(bytes);
    }

    private string GetOpenApiSpec()
    {
        return """
        {
          "openapi": "3.0.1",
          "info": { "title": "AssetRipper Official API", "version": "2.0.0 (Android)" },
          "paths": {
            "/Commands/LoadFile": {
              "post": {
                "tags": ["Commands"],
                "summary": "Picks and loads an APK, .assets, or .bundle file",
                "responses": { "200": { "description": "Success" } }
              }
            },
            "/Commands/LoadFolder": {
              "post": {
                "tags": ["Commands"],
                "summary": "Picks and loads a PC Game Folder (e.g. Game_Data)",
                "responses": { "200": { "description": "Success" } }
              }
            },
            "/Commands/ExportUnityProject": {
              "post": {
                "tags": ["Commands"],
                "summary": "Reconstructs and exports a full Unity Project",
                "responses": { "200": { "description": "Success" } }
              }
            },
            "/Commands/ExportPrimaryContent": {
              "post": {
                "tags": ["Commands"],
                "summary": "Exports raw assets (PNG Textures, GLTF Models, Audio)",
                "responses": { "200": { "description": "Success" } }
              }
            },
            "/Commands/Reset": {
              "post": {
                "tags": ["Commands"],
                "summary": "Resets the engine and unloads current game",
                "responses": { "200": { "description": "Success" } }
              }
            },
            "/api/status": {
              "get": {
                "tags": ["Status"],
                "summary": "Returns engine status and live logs",
                "responses": { "200": { "description": "Success" } }
              }
            }
          }
        }
        """;
    }

    private string GetSwaggerHtml()
    {
        return $$"""
        <!DOCTYPE html>
        <html lang="en">
        <head>
          <meta charset="UTF-8">
          <title>AssetRipper - Swagger UI</title>
          <link rel="stylesheet" href="https://unpkg.com/swagger-ui-dist@5/swagger-ui.css">
          <style>
            html { box-sizing: border-box; overflow: -moz-scrollbars-vertical; overflow-y: scroll; }
            body { margin: 0; background: #1a1a1a; color: #fff; font-family: sans-serif; }
            .swagger-ui .topbar { display: none; }
            .swagger-ui { filter: invert(88%) hue-rotate(180deg); }
            .header-bar { background: #2d2d2d; padding: 12px 20px; display: flex; justify-content: space-between; align-items: center; }
            .badge { background: #478cbf; color: #fff; padding: 4px 8px; border-radius: 4px; font-size: 0.8rem; }
          </style>
        </head>
        <body>
          <div class="header-bar">
            <h2>🎮 AssetRipper Official Web API</h2>
            <span class="badge">Port: {{_port}}</span>
          </div>
          <div id="swagger-ui"></div>
          <script src="https://unpkg.com/swagger-ui-dist@5/swagger-ui-bundle.js"></script>
          <script>
            window.onload = function() {
              SwaggerUIBundle({
                url: "/swagger/v1/swagger.json",
                dom_id: '#swagger-ui',
                presets: [SwaggerUIBundle.presets.apis],
                layout: "BaseLayout"
              });
            };
          </script>
        </body>
        </html>
        """;
    }
}
