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
        public const string DefaultRemoteUrl = "https://claude.ai/artifact/9yqkhfkm7rTQt4wD7TtsfG";

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
        /// 發送 HTTP GET 請求取得字串。若 HttpClient 遇 Cloudflare WAF 防護阻擋 (403 Forbidden)，自動切換至本機 curl.exe 備援。
        /// </summary>
        private static async Task<string> FetchStringWithFallbackAsync(
            string url,
            System.Collections.Generic.Dictionary<string, string>? headers = null,
            CancellationToken ct = default)
        {
            try
            {
                using var req = new HttpRequestMessage(HttpMethod.Get, url);
                req.Headers.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
                if (headers != null)
                {
                    foreach (var kvp in headers)
                    {
                        req.Headers.TryAddWithoutValidation(kvp.Key, kvp.Value);
                    }
                }

                using var resp = await _httpClient.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);
                if (resp.IsSuccessStatusCode)
                {
                    return await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
                }

                if (resp.StatusCode != System.Net.HttpStatusCode.Forbidden)
                {
                    resp.EnsureSuccessStatusCode();
                }
            }
            catch (Exception ex) when (ex is not InvalidOperationException && !ct.IsCancellationRequested)
            {
                // 發生 403 或連線異常時進入 curl.exe 備援
            }

            // 備援：透過系統 curl.exe 發送請求（自動帶入瀏覽器等級之標準 TLS 握手堆疊）
            try
            {
                var psi = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "curl.exe",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };
                psi.ArgumentList.Add("-s");
                psi.ArgumentList.Add("-A");
                psi.ArgumentList.Add("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
                if (headers != null)
                {
                    foreach (var kvp in headers)
                    {
                        psi.ArgumentList.Add("-H");
                        psi.ArgumentList.Add($"{kvp.Key}: {kvp.Value}");
                    }
                }
                psi.ArgumentList.Add(url);

                using var proc = System.Diagnostics.Process.Start(psi);
                if (proc != null)
                {
                    string output = await proc.StandardOutput.ReadToEndAsync().ConfigureAwait(false);
                    await proc.WaitForExitAsync(ct).ConfigureAwait(false);
                    if (proc.ExitCode == 0 && !string.IsNullOrWhiteSpace(output))
                    {
                        return output;
                    }
                }
            }
            catch { }

            throw new InvalidOperationException($"無法取得目標網址內容：{url}");
        }

        /// <summary>
        /// 解析 Claude Artifact 主頁，自動取得沙盒 UUID、Manifest Pin 與當前發布版本號 (ver)，合成最新 Frame URL。
        /// 若傳入已為 direct frame URL 則直接回傳。
        /// </summary>
        public async Task<string> ResolveFrameUrlAsync(
            string artifactUrl,
            Action<string>? onProgress = null,
            CancellationToken ct = default)
        {
            if (artifactUrl.Contains("frame.claudeusercontent.com", StringComparison.OrdinalIgnoreCase))
            {
                return artifactUrl;
            }

            if (!artifactUrl.Contains("claude.ai/artifact/", StringComparison.OrdinalIgnoreCase))
            {
                return artifactUrl;
            }

            onProgress?.Invoke("正在連線至【我的同人攻略圖鑑網】解析沙盒資訊...");

            string html = await FetchStringWithFallbackAsync(artifactUrl, null, ct).ConfigureAwait(false);

            // 1. 取得 data-frame-uuid 與 data-frame-uchost
            var uuidMatch = Regex.Match(html, @"data-frame-uuid=[""']([^""']+)[""']");
            if (!uuidMatch.Success)
            {
                throw new InvalidOperationException("無法自 Claude Artifact 頁面解析 data-frame-uuid。");
            }
            string frameUuid = uuidMatch.Groups[1].Value;

            var uchostMatch = Regex.Match(html, @"data-frame-uchost=[""']([^""']+)[""']");
            string uchost = uchostMatch.Success ? uchostMatch.Groups[1].Value : $"{frameUuid}.frame.claudeusercontent.com";

            // 2. 取得 frame-runtime-pin 與 build-timestamp
            var pinMatch = Regex.Match(html, @"<meta[^>]*name=[""']frame-runtime-pin[""'][^>]*content=[""']([^""']+)[""']", RegexOptions.IgnoreCase);
            if (!pinMatch.Success)
            {
                pinMatch = Regex.Match(html, @"<meta[^>]*content=[""']([^""']+)[""'][^>]*name=[""']frame-runtime-pin[""']", RegexOptions.IgnoreCase);
            }
            string manifestPin = pinMatch.Success ? pinMatch.Groups[1].Value : "manifest.51dd5bc6ff21b6dc.json";

            var tsMatch = Regex.Match(html, @"<meta[^>]*name=[""']build-timestamp[""'][^>]*content=[""']([^""']+)[""']", RegexOptions.IgnoreCase);
            string clientVersion = tsMatch.Success ? tsMatch.Groups[1].Value : "1792948231";

            onProgress?.Invoke("正在查詢 Artifact 最新發布版本...");

            // 3. 呼叫 /api/frame/{uuid} 取得最新版本號 (ver)
            string apiEndpoint = $"https://claude.ai/api/frame/{frameUuid}?via=user_open&bk=initial&actor=id&vt=1&rw=1";
            var apiHeaders = new System.Collections.Generic.Dictionary<string, string>
            {
                ["X-Frame-CP"] = "go",
                ["X-Frame-Platform"] = "web",
                ["X-Frame-Surface"] = "standalone",
                ["X-Frame-Client-Version"] = clientVersion
            };

            string apiJson = await FetchStringWithFallbackAsync(apiEndpoint, apiHeaders, ct).ConfigureAwait(false);
            using var doc = JsonDocument.Parse(apiJson);
            if (!doc.RootElement.TryGetProperty("ver", out var verProp) || string.IsNullOrWhiteSpace(verProp.GetString()))
            {
                throw new InvalidOperationException("Claude Frame API 回傳資料未包含版本號 ver。");
            }
            string ver = verProp.GetString()!;

            string resolvedUrl = $"https://{uchost}/_f/{ver}/?__frame_v={manifestPin}";
            return resolvedUrl;
        }

        /// <summary>
        /// 自第三方網頁更新事件庫。
        /// 執行流程：
        /// 1. 若來源為 Claude Artifact 主頁，自動解析最新沙盒 Frame URL。
        /// 2. HTTP GET 下載網頁 HTML。
        /// 3. 擷取 seed 資料庫種子與 script 運算代碼。
        /// 4. 動態識別按鈕「匯出事件檔」與「匯出路線檔」所綁定之 Function。
        /// 5. 透過 Jint 記憶體引擎執行 Function 取得 JSON。
        /// 6. 驗證資料有效性後，以原子方式寫入 EventBrief.json 與 EventRoute.json。
        /// </summary>
        public async Task<EventArchiveUpdateResult> UpdateFromRemoteAsync(
            string url = DefaultRemoteUrl,
            Action<string>? onProgress = null,
            CancellationToken ct = default)
        {
            string targetUrl = url;
            if (targetUrl.Contains("claude.ai/artifact/", StringComparison.OrdinalIgnoreCase))
            {
                targetUrl = await ResolveFrameUrlAsync(targetUrl, onProgress, ct).ConfigureAwait(false);
            }

            onProgress?.Invoke("正在連線至【我的同人攻略圖鑑網】取得最新資料...");

            string html = await FetchStringWithFallbackAsync(targetUrl, null, ct).ConfigureAwait(false);

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
            if (pRw < 0) throw new InvalidOperationException("無法定位 'const RW =' 運算代碼區塊。");

            int pGtxt = script.IndexOf("const gtxt =", pRw, StringComparison.Ordinal);
            if (pGtxt < 0) throw new InvalidOperationException("無法定位 'const gtxt =' 運算代碼區塊。");

            string rwPtswBlock = script.Substring(pRw, pGtxt - pRw);

            int pRo = script.IndexOf("const routeOf =", StringComparison.Ordinal);
            if (pRo < 0) throw new InvalidOperationException("無法定位 'const routeOf =' 運算代碼區塊。");

            int pRenderPre = script.IndexOf("const renderPre =", pRo, StringComparison.Ordinal);
            if (pRenderPre < 0) throw new InvalidOperationException("無法定位 'const renderPre =' 運算代碼區塊。");

            int pRoEnd = script.LastIndexOf("};", pRenderPre, StringComparison.Ordinal);
            if (pRoEnd < 0) throw new InvalidOperationException("無法定位 routeOf 結束標記。");
            pRoEnd += 2;
            string routeOfBlock = script.Substring(pRo, pRoEnd - pRo);

            // 區塊 2: AB, statsOf, STATN, buildRouteExport, PRE_KIND, preText, buildExport
            int pAb = script.IndexOf("const AB =", StringComparison.Ordinal);
            if (pAb < 0) throw new InvalidOperationException("無法定位 'const AB =' 運算代碼區塊。");

            int pBe = script.IndexOf("const buildExport =", StringComparison.Ordinal);
            if (pBe < 0) throw new InvalidOperationException("無法定位 'const buildExport =' 運算代碼區塊。");

            int pRet = script.IndexOf("return out;", pBe, StringComparison.Ordinal);
            if (pRet < 0) throw new InvalidOperationException("無法定位 'return out;' 運算代碼區塊。");

            int pBeEnd = script.IndexOf("};", pRet, StringComparison.Ordinal);
            if (pBeEnd < 0) throw new InvalidOperationException("無法定位 buildExport 結束標記。");
            pBeEnd += 2;
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

        /// <summary>
        /// 載入本機 ExternalRouteFile 物件。
        /// </summary>
        public ExternalRouteFile? LoadRouteFile()
        {
            try
            {
                EnsureInitialized();
                if (File.Exists(_routePath))
                {
                    return JsonSerializer.Deserialize<ExternalRouteFile>(File.ReadAllText(_routePath));
                }
            }
            catch { }
            return null;
        }

        /// <summary>
        /// 讀取本機路線檔並轉換為 UI 與查詢專用之 EventRegistryItem 列表。
        /// </summary>
        public System.Collections.Generic.List<MyDoujinBot.Models.EventRegistryItem> LoadRegistryItems()
        {
            var result = new System.Collections.Generic.List<MyDoujinBot.Models.EventRegistryItem>();
            var route = LoadRouteFile();
            if (route == null || route.Chains == null) return result;

            foreach (var kvp in route.Chains)
            {
                string chainId = kvp.Key;
                var chain = kvp.Value;
                if (chain.Events == null) continue;

                foreach (var ev in chain.Events)
                {
                    var regItem = new MyDoujinBot.Models.EventRegistryItem
                    {
                        EventId = string.IsNullOrWhiteSpace(ev.Key) ? ev.Title : ev.Key,
                        Title = ev.Title,
                        ChainId = chainId,
                        ChainName = chain.Name,
                        Order = ev.Order
                    };

                    if (ev.Options != null)
                    {
                        foreach (var opt in ev.Options)
                        {
                            regItem.Options.Add(new MyDoujinBot.Models.EventRegistryOption
                            {
                                OptionId = string.IsNullOrWhiteSpace(opt.Key) ? opt.Label : opt.Key,
                                Label = opt.Label,
                                GuideKind = opt.Flags?.Guide?.Kind ?? string.Empty,
                                GuideWhy = opt.Flags?.Guide?.Why ?? string.Empty,
                                HasEncounter = opt.Encounter?.HasEncounter ?? false,
                                OnSuccess = opt.OnSuccess ?? new(),
                                StatsSummary = MyDoujinBot.Models.EventRegistryOption.FormatStatsSummary(opt.OnSuccess)
                            });
                        }
                    }

                    result.Add(regItem);
                }
            }

            return result;
        }
    }
}
