using System;
using System.Collections.Concurrent;
using System.Drawing;
using System.Drawing.Imaging;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using MyDoujinBot.Models;
using SkiaSharp;

namespace MyDoujinBot.Services
{
    public class BattleService
    {
        private const string BaseUrl = HttpService.ApiBaseUrl;
        private const string SiteBaseUrl = "https://mydoujin.online";
        private readonly HttpService _httpService;

        private static readonly HttpClient _imageHttpClient = new()
        {
            Timeout = TimeSpan.FromSeconds(15)
        };

        /// <summary>
        /// 角色頭像記憶體快取（Key: 正規化 URL|targetSize）。
        /// 相同角色立繪只需下載與解碼一次，後續列表重新整理、搜尋與詳細視窗皆可直接共用。
        /// </summary>
        private static readonly ConcurrentDictionary<string, Bitmap> _avatarBitmapCache = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// 進行中的頭像下載工作去重表（避免多位玩家使用同一角色時重複發送相同 HTTP 請求）。
        /// </summary>
        private static readonly ConcurrentDictionary<string, Task<Bitmap?>> _inflightAvatarTasks = new(StringComparer.OrdinalIgnoreCase);

        public BattleService()
        {
            _httpService = new HttpService();
        }

        public async Task<ApiResponse<PlayerProfileResponse>> GetPlayerProfileAsync(string id, string token, CancellationToken cancellationToken = default)
        {
            string url = $"{BaseUrl}/players/{id}";
            return await _httpService.GetAsync<PlayerProfileResponse>(url, token, cancellationToken);
        }

        /// <summary>
        /// 依玩家暱稱搜尋玩家列表（GET /api/players?page=1&amp;limit=10&amp;sort=level&amp;order=desc&amp;name=...）。
        /// </summary>
        public async Task<ApiResponse<PlayersQueryResponse>> SearchPlayersAsync(
            string name,
            string token,
            int page = 1,
            int limit = 10,
            string sort = "level",
            string order = "desc",
            CancellationToken cancellationToken = default)
        {
            string encodedName = Uri.EscapeDataString(name ?? string.Empty);
            string url = $"{BaseUrl}/players?page={page}&limit={limit}&sort={sort}&order={order}&name={encodedName}";
            return await _httpService.GetAsync<PlayersQueryResponse>(url, token, cancellationToken);
        }

        private static string NormalizeAvatarUrl(string imagePath)
        {
            return imagePath.StartsWith("http", StringComparison.OrdinalIgnoreCase)
                ? imagePath.Trim()
                : $"{SiteBaseUrl}{(imagePath.StartsWith("/") ? "" : "/")}{imagePath.Trim()}";
        }

        private static string BuildAvatarCacheKey(string imagePath, int targetSize)
        {
            return $"{NormalizeAvatarUrl(imagePath)}|{targetSize}";
        }

        /// <summary>
        /// 嘗試自記憶體快取同步取得已載入的角色頭像（若已快取則立即回傳，無須非同步等待）。
        /// 回傳之 Bitmap 由快取池統一管理生命週期，呼叫端請勿 Dispose。
        /// </summary>
        public Bitmap? TryGetCachedAvatarBitmap(string? imagePath, int targetSize = 0)
        {
            if (string.IsNullOrWhiteSpace(imagePath))
                return null;

            string key = BuildAvatarCacheKey(imagePath, targetSize);
            return _avatarBitmapCache.TryGetValue(key, out var bmp) ? bmp : null;
        }

        /// <summary>
        /// 透過 GET 取得角色頭像（.webp）並轉換為 WinForms 可用之 32bpp ARGB Bitmap（內建記憶體快取與併發請求去重）。
        /// targetSize &lt;= 0 時保留原始完整解析度與長寬比；targetSize &gt; 0 時依原圖長寬比等比例縮放。
        /// 回傳之 Bitmap 由快取池統一管理生命週期，呼叫端請勿 Dispose。
        /// </summary>
        public async Task<Bitmap?> FetchAvatarBitmapAsync(
            string? imagePath,
            int targetSize = 0,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(imagePath))
                return null;

            string url = NormalizeAvatarUrl(imagePath);
            string cacheKey = $"{url}|{targetSize}";

            if (_avatarBitmapCache.TryGetValue(cacheKey, out var cached))
                return cached;

            var fetchTask = _inflightAvatarTasks.GetOrAdd(cacheKey, _ => DownloadAndDecodeAvatarCoreAsync(url, cacheKey, targetSize));

            try
            {
                return await fetchTask.WaitAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return null;
            }
        }

        private static async Task<Bitmap?> DownloadAndDecodeAvatarCoreAsync(string url, string cacheKey, int targetSize)
        {
            try
            {
                using var req = new HttpRequestMessage(HttpMethod.Get, url);
                req.Headers.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/152.0.0.0 Safari/537.36");
                req.Headers.Referrer = new Uri("https://mydoujin.online/");

                using var resp = await _imageHttpClient.SendAsync(req, HttpCompletionOption.ResponseHeadersRead).ConfigureAwait(false);
                if (!resp.IsSuccessStatusCode)
                    return null;

                byte[] bytes = await resp.Content.ReadAsByteArrayAsync().ConfigureAwait(false);
                if (bytes.Length == 0)
                    return null;

                using var rawBitmap = SKBitmap.Decode(bytes);
                if (rawBitmap == null || rawBitmap.Width <= 0 || rawBitmap.Height <= 0)
                    return null;

                int width = rawBitmap.Width;
                int height = rawBitmap.Height;
                if (targetSize > 0)
                {
                    double ratio = Math.Min((double)targetSize / rawBitmap.Width, (double)targetSize / rawBitmap.Height);
                    width = Math.Max(1, (int)Math.Round(rawBitmap.Width * ratio));
                    height = Math.Max(1, (int)Math.Round(rawBitmap.Height * ratio));
                }

                var info = new SKImageInfo(width, height, SKColorType.Bgra8888, SKAlphaType.Premul);
                using var scaledBitmap = new SKBitmap(info);
                rawBitmap.ScalePixels(scaledBitmap, SKFilterQuality.High);

                var bmp = new Bitmap(width, height, PixelFormat.Format32bppPArgb);
                var bmpData = bmp.LockBits(
                    new Rectangle(0, 0, width, height),
                    ImageLockMode.WriteOnly,
                    PixelFormat.Format32bppPArgb);

                try
                {
                    byte[] pixelBuffer = scaledBitmap.Bytes;
                    Marshal.Copy(pixelBuffer, 0, bmpData.Scan0, Math.Min(pixelBuffer.Length, bmpData.Stride * height));
                }
                finally
                {
                    bmp.UnlockBits(bmpData);
                }

                _avatarBitmapCache[cacheKey] = bmp;
                return bmp;
            }
            catch
            {
                return null;
            }
            finally
            {
                _inflightAvatarTasks.TryRemove(cacheKey, out _);
            }
        }

        public async Task<ApiResponse<BattleActionResponse>> ChallengeAsync(string id, string token, CancellationToken cancellationToken = default)
        {
            string url = $"{BaseUrl}/players/{id}/challenge";
            // Empty body for POST
            return await _httpService.PostAsync<BattleActionResponse>(url, token, null!, cancellationToken);
        }

        public async Task<ApiResponse<BattleActionResponse>> ChadoAsync(string id, string token, CancellationToken cancellationToken = default)
        {
            string url = $"{BaseUrl}/players/{id}/chado";
            // Empty body for POST
            return await _httpService.PostAsync<BattleActionResponse>(url, token, null!, cancellationToken);
        }

        public async Task<ApiResponse<BattleReportResponse>> GetBattleReportAsync(string reportId, string token, CancellationToken cancellationToken = default)
        {
            string url = $"{BaseUrl}/battle-reports/{reportId}";
            return await _httpService.GetAsync<BattleReportResponse>(url, token, cancellationToken);
        }
    }
}
