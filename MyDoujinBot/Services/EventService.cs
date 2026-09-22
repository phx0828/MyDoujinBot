using System;
using System.Threading;
using System.Threading.Tasks;
using MyDoujinBot.Models;

namespace MyDoujinBot.Services
{
    /// <summary>
    /// 事件 API 的業務邏輯服務。
    /// 專注於事件選擇 API 端點邏輯，底層網路通訊完全委託給集中的 HttpService。
    /// </summary>
    public class EventService : IDisposable
    {
        private const string EventEndpoint = "https://mydoujin-backend.onrender.com/api/action/resolve";
        private readonly HttpService _httpService;

        public EventService(HttpService? httpService = null)
        {
            _httpService = httpService ?? new HttpService();
        }

        public async Task<EventActionResult> SelectOptionAsync(
            string token,
            string optionId,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(optionId))
                return EventActionResult.Fail("未選擇事件選項。");

            var apiResponse = await _httpService.PostAsync<EventResult>(
                EventEndpoint, token, new { optionId }, cancellationToken);

            if (apiResponse.IsCancelled)
                return EventActionResult.Cancelled();

            if (!apiResponse.IsSuccess)
                return EventActionResult.Fail(apiResponse.ErrorMessage ?? "未知錯誤", apiResponse.IsUnauthorized);

            return EventActionResult.Success(apiResponse.Data!);
        }

        public void Dispose()
        {
            _httpService.Dispose();
            GC.SuppressFinalize(this);
        }
    }

    public class EventActionResult
    {
        public bool IsSuccess { get; private set; }
        public bool IsCancelled { get; private set; }
        public bool IsUnauthorized { get; private set; }
        public string? ErrorMessage { get; private set; }
        public EventResult? Result { get; private set; }

        private EventActionResult() { }

        public static EventActionResult Success(EventResult result) =>
            new() { IsSuccess = true, Result = result };

        public static EventActionResult Fail(string errorMessage, bool isUnauthorized = false) =>
            new() { IsSuccess = false, ErrorMessage = errorMessage, IsUnauthorized = isUnauthorized };

        public static EventActionResult Cancelled() =>
            new() { IsCancelled = true };
    }
}
