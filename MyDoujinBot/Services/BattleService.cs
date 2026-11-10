using System;
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

        /// <summary>
        /// 透過 GET 取得角色頭像（.webp）並於記憶體直接轉換為 WinForms 可用之 32bpp ARGB Bitmap（不落地快取）。
        /// </summary>
        public async Task<Bitmap?> FetchAvatarBitmapAsync(
            string? imagePath,
            int targetSize = 44,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(imagePath))
                return null;

            try
            {
                string url = imagePath.StartsWith("http", StringComparison.OrdinalIgnoreCase)
                    ? imagePath
                    : $"{SiteBaseUrl}{(imagePath.StartsWith("/") ? "" : "/")}{imagePath}";

                using var req = new HttpRequestMessage(HttpMethod.Get, url);
                req.Headers.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/152.0.0.0 Safari/537.36");
                req.Headers.Referrer = new Uri("https://mydoujin.online/");

                using var resp = await _imageHttpClient.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
                if (!resp.IsSuccessStatusCode)
                    return null;

                byte[] bytes = await resp.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
                if (bytes.Length == 0)
                    return null;

                using var rawBitmap = SKBitmap.Decode(bytes);
                if (rawBitmap == null)
                    return null;

                int width = targetSize > 0 ? targetSize : rawBitmap.Width;
                int height = targetSize > 0 ? targetSize : rawBitmap.Height;

                var info = new SKImageInfo(width, height, SKColorType.Bgra8888, SKAlphaType.Premul);
                using var scaledBitmap = new SKBitmap(info);
                rawBitmap.ScalePixels(scaledBitmap, SKFilterQuality.Medium);

                var bmp = new Bitmap(width, height, PixelFormat.Format32bppPArgb);
                var bmpData = bmp.LockBits(
                    new Rectangle(0, 0, width, height),
                    ImageLockMode.WriteOnly,
                    PixelFormat.Format32bppPArgb);

                try
                {
                    int byteCount = scaledBitmap.ByteCount;
                    byte[] pixelBuffer = scaledBitmap.Bytes;
                    Marshal.Copy(pixelBuffer, 0, bmpData.Scan0, Math.Min(pixelBuffer.Length, bmpData.Stride * height));
                }
                finally
                {
                    bmp.UnlockBits(bmpData);
                }

                return bmp;
            }
            catch
            {
                return null;
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
