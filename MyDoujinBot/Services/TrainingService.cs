using System;
using System.Threading;
using System.Threading.Tasks;
using MyDoujinBot.Models;

namespace MyDoujinBot.Services
{
    /// <summary>
    /// 訓練 API 的業務邏輯服務。
    /// 專注於訓練 API 端點邏輯，底層網路通訊完全委託給集中的 HttpService。
    /// </summary>
    public class TrainingService : IDisposable
    {
        private const string TrainingEndpoint = "https://mydoujin-backend.onrender.com/api/action/explore";
        private readonly HttpService _httpService;

        public TrainingService(HttpService? httpService = null)
        {
            _httpService = httpService ?? new HttpService();
        }

        public async Task<TrainingResult> ExecuteTrainingAsync(
            string token,
            string actionId,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(actionId))
                return TrainingResult.Fail("未選擇訓練行動。");

            var apiResponse = await _httpService.PostAsync<TrainingResponse>(
                TrainingEndpoint, token, new { actionId }, cancellationToken);

            if (apiResponse.IsCancelled)
                return TrainingResult.Cancelled();

            if (!apiResponse.IsSuccess)
                return TrainingResult.Fail(apiResponse.ErrorMessage ?? "未知錯誤", apiResponse.IsUnauthorized);

            return TrainingResult.Success(apiResponse.Data!);
        }

        public void Dispose()
        {
            _httpService.Dispose();
            GC.SuppressFinalize(this);
        }
    }

    public class TrainingResult
    {
        public bool IsSuccess { get; private set; }
        public bool IsCancelled { get; private set; }
        public bool IsUnauthorized { get; private set; }
        public string? ErrorMessage { get; private set; }
        public TrainingResponse? Response { get; private set; }

        private TrainingResult() { }

        public static TrainingResult Success(TrainingResponse response) =>
            new() { IsSuccess = true, Response = response };

        public static TrainingResult Fail(string errorMessage, bool isUnauthorized = false) =>
            new() { IsSuccess = false, ErrorMessage = errorMessage, IsUnauthorized = isUnauthorized };

        public static TrainingResult Cancelled() =>
            new() { IsCancelled = true };
    }
}
