using System;
using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Jint;
using MyDoujinBot.Models.ExternalEvents;

namespace MyDoujinBot.Services
{
    /// <summary>
    /// 事件庫更新結果。
    /// </summary>
    public class EventArchiveUpdateResult
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public int PreviousBriefEventCount { get; set; }
        public int PreviousRouteChainCount { get; set; }
        public int PreviousRouteEventCount { get; set; }
        public int BriefEventCount { get; set; }
        public int RouteChainCount { get; set; }
        public int RouteEventCount { get; set; }
        public int BriefEventDiff => BriefEventCount - PreviousBriefEventCount;
        public int RouteChainDiff => RouteChainCount - PreviousRouteChainCount;
        public int RouteEventDiff => RouteEventCount - PreviousRouteEventCount;
        public System.Collections.Generic.List<string> NewChainNames { get; set; } = new();
        public DateTime UpdatedAt { get; set; }
    }

    /// <summary>
    /// 事件庫中繼狀態。
    /// </summary>
    public class EventArchiveMetadata
    {
        public int Version { get; set; } = 2;
        public DateTime? LastUpdatedAt { get; set; }
        public string Site { get; set; } = string.Empty;
        public string Author { get; set; } = string.Empty;
        public int BriefEventCount { get; set; }
        public int RouteChainCount { get; set; }
        public string SourceInfo { get; set; } = string.Empty;
    }

    /// <summary>
    /// 第三方事件檔案庫管理與更新服務。
    /// 負責本機事件資料庫（data/events/）之初始化、出廠預設防呆複製、
    /// 以及自第三方網站動態下載網頁並透過 Jint 執行計算函式完成更新。
    /// </summary>
    public class EventArchiveService
    {
        public const string DefaultRemoteUrl = "https://48b8ce48-6995-4245-9468-c61c2b43540b.frame.claudeusercontent.com/_f/1792312812-93cc/?__frame_v=manifest.99164b231888b1a4.json";

        private readonly string _eventsDir;
        private readonly string _defaultsDir;
        private readonly string _briefPath;
        private readonly string _routePath;
        private readonly string _metadataPath;

        private static readonly HttpClient _httpClient = new()
        {
            Timeout = TimeSpan.FromSeconds(30)
        };

        private static readonly JsonSerializerOptions _jsonOptions = new()
        {
            WriteIndented = true,
            PropertyNameCaseInsensitive = true
        };

        public EventArchiveService(string? customBaseDir = null)
        {
            string baseDir = !string.IsNullOrWhiteSpace(customBaseDir)
                ? customBaseDir
                : AppDomain.CurrentDomain.BaseDirectory;
            _eventsDir = Path.Combine(baseDir, "data", "events");
            _defaultsDir = Path.Combine(baseDir, "data", "defaults");
            _briefPath = Path.Combine(_eventsDir, "EventBrief.json");
            _routePath = Path.Combine(_eventsDir, "EventRoute.json");
            _metadataPath = Path.Combine(_eventsDir, "metadata.json");
        }

        public string EventsDirectory => _eventsDir;
        public string BriefPath => _briefPath;
        public string RoutePath => _routePath;

        /// <summary>
        /// 確保事件資料庫目錄與檔案已就緒。
        /// 若使用者為首次啟動或本機檔案缺失，自動自 data/defaults/ 或內嵌資源中複製釋放預設資料。
        /// </summary>
        public void EnsureInitialized()
        {
            try
            {
                if (!Directory.Exists(_eventsDir))
                {
                    Directory.CreateDirectory(_eventsDir);
                }

                // 1. 檢查 EventBrief.json
                if (!File.Exists(_briefPath))
                {
                    RestoreDefaultFile("DefaultEventBrief.json", _briefPath);
                }

                // 2. 檢查 EventRoute.json
                if (!File.Exists(_routePath))
                {
                    RestoreDefaultFile("DefaultEventRoute.json", _routePath);
                }

                // 3. 檢查 metadata.json
                if (!File.Exists(_metadataPath))
                {
                    SaveMetadata(new EventArchiveMetadata
                    {
                        Version = 2,
                        LastUpdatedAt = File.Exists(_briefPath) ? File.GetLastWriteTime(_briefPath) : DateTime.Now,
                        SourceInfo = "出廠預設資料庫",
                        Site = "我的同人攻略圖鑑網",
                        Author = "赤羽和一（Discord）",
                        BriefEventCount = GetBriefEventCountInternal(),
                        RouteChainCount = GetRouteChainCountInternal()
                    });
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[EventArchiveService] 初始化事件庫異常: {ex.Message}");
            }
        }

        /// <summary>
        /// 取得目前本地事件庫之狀態與摘要文字。
        /// </summary>
        public EventArchiveMetadata GetMetadata()
        {
            EnsureInitialized();
            try
            {
                if (File.Exists(_metadataPath))
                {
                    var json = File.ReadAllText(_metadataPath);
                    var meta = JsonSerializer.Deserialize<EventArchiveMetadata>(json, _jsonOptions);
                    if (meta != null) return meta;
                }
            }
            catch { }

            return new EventArchiveMetadata
            {
                BriefEventCount = GetBriefEventCountInternal(),
                RouteChainCount = GetRouteChainCountInternal(),
                LastUpdatedAt = File.Exists(_briefPath) ? File.GetLastWriteTime(_briefPath) : null,
                SourceInfo = "本機檔案庫"
            };
        }

        /// <summary>
        /// 載入目前的簡要事件檔。
        /// </summary>
        public ExternalEventBriefFile? LoadBrief()
        {
            EnsureInitialized();
            if (!File.Exists(_briefPath)) return null;
            string json = File.ReadAllText(_briefPath);
            return JsonSerializer.Deserialize<ExternalEventBriefFile>(json);
        }

        /// <summary>
        /// 載入目前的詳細路線檔。
        /// </summary>
        public ExternalRouteFile? LoadRoute()
        {
            EnsureInitialized();
            if (!File.Exists(_routePath)) return null;
            string json = File.ReadAllText(_routePath);
            return JsonSerializer.Deserialize<ExternalRouteFile>(json);
        }

        /// <summary>
        /// 自第三方網頁更新事件庫。
        /// 執行流程：
        /// 1. HTTP GET 下載網頁 HTML。
        /// 2. 擷取 seed 資料庫種子與 script 運算代碼。
        /// 3. 動態識別按鈕「匯出事件檔」與「匯出路線檔」所綁定之 Function。
        /// 4. 透過 Jint 記憶體引擎執行 Function 取得 JSON。
        /// 5. 驗證資料有效性後，以原子方式寫入 EventBrief.json 與 EventRoute.json。
        /// </summary>
        public async Task<EventArchiveUpdateResult> UpdateFromRemoteAsync(
            string url = DefaultRemoteUrl,
            Action<string>? onProgress = null,
            CancellationToken ct = default)
        {
            onProgress?.Invoke("正在連線至【我的同仁攻略圖鑑網】取得最新資料...");

            using var req = new HttpRequestMessage(HttpMethod.Get, url);
            req.Headers.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");

            using var resp = await _httpClient.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);
            resp.EnsureSuccessStatusCode();

            string html = await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);

            onProgress?.Invoke("正在剖析網頁結構...");

            // 1. 擷取 <script id="seed">
            var seedMatch = Regex.Match(html, @"<script id=""seed"" type=""application/json"">([\s\S]*?)</script>");
            if (!seedMatch.Success)
            {
                throw new InvalidOperationException("網頁中未找到 id=\"seed\" 的資料庫種子區塊。");
            }
            string seedJson = seedMatch.Groups[1].Value.Trim();

            // 2. 找到按鈕「匯出事件檔」與「匯出路線檔」的按鈕 ID
            string briefBtnId = "exp";
            string routeBtnId = "expr";

            var expBtnMatch = Regex.Match(html, @"<button[^>]*id=[""']([^""']+)[""'][^>]*>[^<]*匯出事件檔[^<]*</button>");
            if (expBtnMatch.Success) briefBtnId = expBtnMatch.Groups[1].Value;

            var exprBtnMatch = Regex.Match(html, @"<button[^>]*id=[""']([^""']+)[""'][^>]*>[^<]*匯出路線檔[^<]*</button>");
            if (exprBtnMatch.Success) routeBtnId = exprBtnMatch.Groups[1].Value;

            // 3. 找到 wire 綁定的函式名稱
            string briefFuncName = "buildExport";
            string routeFuncName = "buildRouteExport";

            var fnBriefMatch = Regex.Match(html, @"wire\(\$\([""']" + Regex.Escape(briefBtnId) + @"[""']\),\s*[""'][^""']*[""'],\s*([a-zA-Z0-9_]+)\)");
            if (fnBriefMatch.Success) briefFuncName = fnBriefMatch.Groups[1].Value;

            var fnRouteMatch = Regex.Match(html, @"wire\(\$\([""']" + Regex.Escape(routeBtnId) + @"[""']\),\s*[""'][^""']*[""'],\s*([a-zA-Z0-9_]+)\)");
            if (fnRouteMatch.Success) routeFuncName = fnRouteMatch.Groups[1].Value;

            onProgress?.Invoke($"正在定位匯出資料函式...");

            // 4. 擷取第 3 個 <script> 中的運算模組
            var scriptMatches = Regex.Matches(html, @"<script([^>]*)>([\s\S]*?)</script>");
            string logicScript = string.Empty;
            foreach (Match sm in scriptMatches)
            {
                string body = sm.Groups[2].Value;
                if (body.Contains("buildExport") || body.Contains("buildRouteExport"))
                {
                    logicScript = body;
                    break;
                }
            }

            if (string.IsNullOrEmpty(logicScript))
            {
                throw new InvalidOperationException("網頁中未找到包含匯出運算函式的指令碼區塊。");
            }

            string calculationJs = ExtractCalculationJs(logicScript);

            onProgress?.Invoke("正在啟動 Jint 引擎運算事件庫與前置圖譜...");

            // 5. 透過 Jint 記憶體引擎執行
            var engine = new Engine();
            engine.SetValue("seedJsonStr", seedJson);

            string runnerJs = $@"
const model = JSON.parse(seedJsonStr);

{calculationJs}

function runExportBrief() {{
    return JSON.stringify({briefFuncName}());
}}

function runExportRoute() {{
    return JSON.stringify({routeFuncName}());
}}
";
            engine.Execute(runnerJs);

            string generatedBriefJson = engine.Evaluate("runExportBrief()").AsString();
            string generatedRouteJson = engine.Evaluate("runExportRoute()").AsString();

            onProgress?.Invoke("正在驗證產出結構與資料完整性...");

            // 6. 驗證產出是否能正確反序列化至現有規格模型
            var briefObj = JsonSerializer.Deserialize<ExternalEventBriefFile>(generatedBriefJson)
                ?? throw new InvalidOperationException("產出之 EventBrief 反序列化結果為空。");

            var routeObj = JsonSerializer.Deserialize<ExternalRouteFile>(generatedRouteJson)
                ?? throw new InvalidOperationException("產出之 EventRoute 反序列化結果為空。");

            if (briefObj.Count == 0 || routeObj.Chains.Count == 0)
            {
                throw new InvalidOperationException($"產出資料異常 (事件數: {briefObj.Count}, 路線數: {routeObj.Chains.Count})。");
            }

            // 讀取更新前的本機資料庫狀態（用於計算增減數量）
            int prevBriefCount = 0;
            int prevChainCount = 0;
            int prevRouteEventCount = 0;
            var prevChainKeys = new System.Collections.Generic.HashSet<string>(StringComparer.OrdinalIgnoreCase);

            try
            {
                var prevBrief = LoadBrief();
                if (prevBrief != null)
                {
                    prevBriefCount = prevBrief.Count;
                }

                var prevRoute = LoadRoute();
                if (prevRoute != null)
                {
                    prevChainCount = prevRoute.Chains.Count;
                    foreach (var kvp in prevRoute.Chains)
                    {
                        prevChainKeys.Add(kvp.Key);
                        prevRouteEventCount += kvp.Value.Events.Count;
                    }
                }
            }
            catch { }

            onProgress?.Invoke("正在儲存至本機檔案庫 (data/events/)...");

            // 7. 原子寫入 (先寫入 .tmp 再覆寫目標檔)
            SaveAtomic(_briefPath, generatedBriefJson);
            SaveAtomic(_routePath, generatedRouteJson);

            int totalRouteEvents = 0;
            var newChainNames = new System.Collections.Generic.List<string>();
            foreach (var kvp in routeObj.Chains)
            {
                totalRouteEvents += kvp.Value.Events.Count;
                if (!prevChainKeys.Contains(kvp.Key))
                {
                    string displayName = !string.IsNullOrWhiteSpace(kvp.Value.Name)
                        ? kvp.Value.Name
                        : kvp.Key;
                    newChainNames.Add(displayName);
                }
            }

            int briefDiff = briefObj.Count - prevBriefCount;
            int chainDiff = routeObj.Chains.Count - prevChainCount;
            int routeEventDiff = totalRouteEvents - prevRouteEventCount;

            static string FormatDelta(int diff) => diff switch
            {
                > 0 => $"+{diff}",
                < 0 => $"{diff}",
                _ => "無增減"
            };

            string briefDiffText = FormatDelta(briefDiff);
            string chainDiffText = FormatDelta(chainDiff);
            string msg = $"更新成功！共載入 {briefObj.Count} 個事件（{briefDiffText}）、{routeObj.Chains.Count} 條路線（{chainDiffText}，共 {totalRouteEvents} 個節點）。";

            var now = DateTime.Now;
            var meta = new EventArchiveMetadata
            {
                Version = routeObj.About.Version > 0 ? routeObj.About.Version : 2,
                LastUpdatedAt = now,
                Site = routeObj.About.Site,
                Author = routeObj.About.Author,
                BriefEventCount = briefObj.Count,
                RouteChainCount = routeObj.Chains.Count,
                SourceInfo = $"{routeObj.About.Site} ({routeObj.About.DataDate})"
            };
            SaveMetadata(meta);

            return new EventArchiveUpdateResult
            {
                Success = true,
                Message = msg,
                PreviousBriefEventCount = prevBriefCount,
                PreviousRouteChainCount = prevChainCount,
                PreviousRouteEventCount = prevRouteEventCount,
                BriefEventCount = briefObj.Count,
                RouteChainCount = routeObj.Chains.Count,
                RouteEventCount = totalRouteEvents,
                NewChainNames = newChainNames,
                UpdatedAt = now
            };
        }

        /// <summary>
        /// 從完整腳本中提取計算相關函式，排除與 DOM 相關之繪製邏輯。
        /// </summary>
        private static string ExtractCalculationJs(string script)
        {
            // 區塊 1: RW, ptsW, pOf, routeOf
            int pRw = script.IndexOf("const RW =", StringComparison.Ordinal);
            int pGtxt = script.IndexOf("const gtxt =", pRw, StringComparison.Ordinal);
            string rwPtswBlock = script.Substring(pRw, pGtxt - pRw);

            int pRo = script.IndexOf("const routeOf =", StringComparison.Ordinal);
            int pRenderPre = script.IndexOf("const renderPre =", pRo, StringComparison.Ordinal);
            int pRoEnd = script.LastIndexOf("};", pRenderPre, StringComparison.Ordinal) + 2;
            string routeOfBlock = script.Substring(pRo, pRoEnd - pRo);

            // 區塊 2: AB, statsOf, STATN, buildRouteExport, PRE_KIND, preText, buildExport
            int pAb = script.IndexOf("const AB =", StringComparison.Ordinal);
            int pBe = script.IndexOf("const buildExport =", StringComparison.Ordinal);
            int pRet = script.IndexOf("return out;", pBe, StringComparison.Ordinal);
            int pBeEnd = script.IndexOf("};", pRet, StringComparison.Ordinal) + 2;
            string exportBlock = script.Substring(pAb, pBeEnd - pAb);

            return rwPtswBlock + "\n" + routeOfBlock + "\n" + exportBlock;
        }

        /// <summary>
        /// 原子寫入檔案：先寫入 .tmp 暫存檔，成功後再覆寫，避免斷電或中止造成損毀。
        /// </summary>
        private static void SaveAtomic(string targetPath, string content)
        {
            string tmpPath = targetPath + ".tmp";
            File.WriteAllText(tmpPath, content);
            File.Move(tmpPath, targetPath, overwrite: true);
        }

        private void SaveMetadata(EventArchiveMetadata meta)
        {
            try
            {
                string json = JsonSerializer.Serialize(meta, _jsonOptions);
                SaveAtomic(_metadataPath, json);
            }
            catch { }
        }

        private void RestoreDefaultFile(string defaultFileName, string targetPath)
        {
            // 優先從 data/defaults/ 實體目錄複製
            string sourcePath = Path.Combine(_defaultsDir, defaultFileName);
            if (File.Exists(sourcePath))
            {
                File.Copy(sourcePath, targetPath, overwrite: true);
                return;
            }

            // 次選：自 EmbeddedResource 內嵌資源中釋放
            try
            {
                var assembly = Assembly.GetExecutingAssembly();
                string resourceName = $"MyDoujinBot.data.defaults.{defaultFileName}";
                using var stream = assembly.GetManifestResourceStream(resourceName);
                if (stream != null)
                {
                    using var fileStream = File.Create(targetPath);
                    stream.CopyTo(fileStream);
                    return;
                }
            }
            catch { }

            // 備援：往上尋找專案目錄中的 data/defaults 或 docs/
            string[] probePaths =
            {
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "data", "defaults", defaultFileName),
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "docs", defaultFileName),
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "docs", defaultFileName)
            };
            foreach (var probe in probePaths)
            {
                try
                {
                    string fullProbe = Path.GetFullPath(probe);
                    if (File.Exists(fullProbe))
                    {
                        File.Copy(fullProbe, targetPath, overwrite: true);
                        return;
                    }
                }
                catch { }
            }
        }

        private int GetBriefEventCountInternal()
        {
            try
            {
                if (File.Exists(_briefPath))
                {
                    var brief = JsonSerializer.Deserialize<ExternalEventBriefFile>(File.ReadAllText(_briefPath));
                    return brief?.Count ?? 0;
                }
            }
            catch { }
            return 0;
        }

        private int GetRouteChainCountInternal()
        {
            try
            {
                if (File.Exists(_routePath))
                {
                    var route = JsonSerializer.Deserialize<ExternalRouteFile>(File.ReadAllText(_routePath));
                    return route?.Chains.Count ?? 0;
                }
            }
            catch { }
            return 0;
        }
    }
}
