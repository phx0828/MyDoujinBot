using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace MyDoujinBot.Services
{
    /// <summary>
    /// 獨立的 HTTP 通訊服務。
    /// 負責：統一管理 HttpClient、HTTP Headers (User-Agent, Origin, Referer, Authorization, Content-Type)、
    /// JSON 序列化/反序列化與 HTTP 錯誤處理。
    /// 
    /// 為什麼要獨立這個 Service？
    /// 未來只要調整 Header、新增自訂 Header 或修改通用網路請求機制，
    /// 只需要修改這個檔案，不需要在 TrainingService 和 EventService 重複修改。
    /// </summary>
    public class HttpService : IDisposable
    {
        private readonly HttpClient _httpClient;

        private static readonly JsonSerializerOptions _jsonOptions = new()
        {
            PropertyNameCaseInsensitive = true
        };

        public HttpService()
        {
            _httpClient = new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(30)
            };
        }

        /// <summary>
        /// 泛型 POST JSON 請求方法
        /// C# 泛型 (Generics) 說明：
        /// <TResponse> 是一個型別佔位符，呼叫時傳入期望反序列化的類別 (如 TrainingResponse 或 EventResult)。
        /// </summary>
        public async Task<ApiResponse<TResponse>> PostAsync<TResponse>(
            string endpoint,
            string token,
            object body,
            CancellationToken cancellationToken) where TResponse : class
        {
            var cleanToken = CleanToken(token);
            if (string.IsNullOrWhiteSpace(cleanToken))
                return ApiResponse<TResponse>.Fail("Token 未設定，請輸入 Bearer Token。");

            try
            {
                var json = JsonSerializer.Serialize(body);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var request = new HttpRequestMessage(HttpMethod.Post, endpoint)
                {
                    Content = content
                };

                // =====================================================================
                // 統一 Header 設定區域（所有 API 請求均會自動套用）
                // 未來要修改或新增 Header，只需要修改這裡！
                // =====================================================================
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", cleanToken);
                request.Headers.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/152.0.0.0 Safari/537.36");
                request.Headers.Add("Origin", "https://mydoujin.online");
                request.Headers.Referrer = new Uri("https://mydoujin.online/");

                var response = await _httpClient.SendAsync(request, cancellationToken);

                if (!response.IsSuccessStatusCode)
                {
                    return await HandleHttpErrorAsync<TResponse>(response);
                }

                var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
                var data = JsonSerializer.Deserialize<TResponse>(responseBody, _jsonOptions);

                if (data == null)
                    return ApiResponse<TResponse>.Fail("API 回傳空白 Response。");

                return ApiResponse<TResponse>.Success(data);
            }
            catch (OperationCanceledException)
            {
                if (cancellationToken.IsCancellationRequested)
                    return ApiResponse<TResponse>.Cancelled();
                else
                    return ApiResponse<TResponse>.Fail("Request 超時（Timeout），請檢查網路連線。");
            }
            catch (HttpRequestException ex)
            {
                return ApiResponse<TResponse>.Fail($"網路錯誤：{ex.Message}");
            }
            catch (JsonException ex)
            {
                return ApiResponse<TResponse>.Fail($"Response 格式錯誤：{ex.Message}");
            }
            catch (Exception ex)
            {
                return ApiResponse<TResponse>.Fail($"未知錯誤：{ex.GetType().Name} - {ex.Message}");
            }
        }

        private static string CleanToken(string token)
        {
            if (string.IsNullOrWhiteSpace(token)) return string.Empty;
            token = token.Trim();
            if (token.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            {
                token = token[7..].Trim();
            }
            return token;
        }

        private static async Task<ApiResponse<TResponse>> HandleHttpErrorAsync<TResponse>(HttpResponseMessage response)
            where TResponse : class
        {
            string errorDetail;
            try
            {
                var body = await response.Content.ReadAsStringAsync();
                errorDetail = body.Length > 200 ? body[..200] + "..." : body;
            }
            catch
            {
                errorDetail = string.Empty;
            }

            var statusCode = (int)response.StatusCode;
            var statusName = response.StatusCode switch
            {
                HttpStatusCode.Unauthorized => "Unauthorized（Token 無效或已過期）",
                HttpStatusCode.Forbidden => "Forbidden（沒有權限）",
                HttpStatusCode.BadRequest => "Bad Request（Request 格式錯誤）",
                HttpStatusCode.NotFound => "Not Found",
                HttpStatusCode.TooManyRequests => "Too Many Requests（請求太頻繁）",
                HttpStatusCode.InternalServerError => "Server Error（伺服器內部錯誤）",
                HttpStatusCode.ServiceUnavailable => "Service Unavailable（伺服器暫時停機）",
                _ => response.StatusCode.ToString()
            };

            var message = string.IsNullOrWhiteSpace(errorDetail)
                ? $"HTTP {statusCode} {statusName}"
                : $"HTTP {statusCode} {statusName}：{errorDetail}";

            bool isUnauthorized = response.StatusCode == HttpStatusCode.Unauthorized;
            return ApiResponse<TResponse>.Fail(message, isUnauthorized);
        }

        public void Dispose()
        {
            _httpClient.Dispose();
            GC.SuppressFinalize(this);
        }
    }

    /// <summary>
    /// API 請求統一結果封裝類別
    /// </summary>
    public class ApiResponse<T> where T : class
    {
        public bool IsSuccess { get; private set; }
        public bool IsCancelled { get; private set; }
        public bool IsUnauthorized { get; private set; }
        public string? ErrorMessage { get; private set; }
        public T? Data { get; private set; }

        private ApiResponse() { }

        public static ApiResponse<T> Success(T data) =>
            new() { IsSuccess = true, Data = data };

        public static ApiResponse<T> Fail(string errorMessage, bool isUnauthorized = false) =>
            new() { IsSuccess = false, ErrorMessage = errorMessage, IsUnauthorized = isUnauthorized };

        public static ApiResponse<T> Cancelled() =>
            new() { IsCancelled = true };
    }
}
