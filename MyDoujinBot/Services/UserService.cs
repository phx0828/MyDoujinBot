using System;
using System.Threading;
using System.Threading.Tasks;
using MyDoujinBot.Models;

namespace MyDoujinBot.Services
{
    /// <summary>
    /// 使用者資訊與驗證服務。
    /// 負責呼叫 users/me, characters/active, action/status 進行 Token 驗證與資料拉取，
    /// 並於成功時自動同步寫入全域 UserContext。
    /// </summary>
    public class UserService : IDisposable
    {
        private const string MeEndpoint = $"{HttpService.ApiBaseUrl}/users/me";
        private const string ActiveCharacterEndpoint = $"{HttpService.ApiBaseUrl}/characters/active";
        private const string CharactersEndpoint = $"{HttpService.ApiBaseUrl}/characters";
        private const string ActionStatusEndpoint = $"{HttpService.ApiBaseUrl}/action/status";
        private const string FloorRewardEndpoint = $"{HttpService.ApiBaseUrl}/action/floor-reward";

        private readonly HttpService _httpService;
        private readonly bool _ownsHttpService;

        public UserService(HttpService? httpService = null)
        {
            if (httpService != null)
            {
                _httpService = httpService;
                _ownsHttpService = false;
            }
            else
            {
                _httpService = new HttpService();
                _ownsHttpService = true;
            }
        }

        /// <summary>
        /// 驗證 Token 並依序呼叫 users/me, characters/active, action/status 三隻 API。
        /// 必須三隻皆成功才視為有效已驗證，並同步寫入 UserContext。
        /// </summary>
        public async Task<UserValidationResult> ValidateAndFetchProfileAsync(string token, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(token))
            {
                UserContext.Clear();
                return UserValidationResult.Fail("Token 未設定，請輸入 Bearer Token。");
            }

            // 1. 呼叫 users/me 取得玩家個人資料
            var meRes = await _httpService.GetAsync<UserMeResponse>(MeEndpoint, token, cancellationToken);
            if (cancellationToken.IsCancellationRequested)
                return UserValidationResult.Cancelled();

            if (!meRes.IsSuccess || meRes.Data == null)
            {
                UserContext.Clear();
                return UserValidationResult.Fail($"驗證個人資料失敗：{meRes.ErrorMessage ?? "未知錯誤"}", meRes.IsUnauthorized);
            }

            // 2. 取得目前出戰角色資訊
            var activeRes = await _httpService.GetAsync<ActiveCharacterResponse>(ActiveCharacterEndpoint, token, cancellationToken);
            if (cancellationToken.IsCancellationRequested)
                return UserValidationResult.Cancelled();

            if (!activeRes.IsSuccess || activeRes.Data == null)
            {
                UserContext.Clear();
                return UserValidationResult.Fail($"取得出戰角色失敗：{activeRes.ErrorMessage ?? "未知錯誤"}", activeRes.IsUnauthorized);
            }

            // 3. 取得目前冷卻與行動狀態
            var statusRes = await _httpService.GetAsync<ActionStatusResponse>(ActionStatusEndpoint, token, cancellationToken);
            if (cancellationToken.IsCancellationRequested)
                return UserValidationResult.Cancelled();

            if (!statusRes.IsSuccess || statusRes.Data == null)
            {
                UserContext.Clear();
                return UserValidationResult.Fail($"取得行動狀態失敗：{statusRes.ErrorMessage ?? "未知錯誤"}", statusRes.IsUnauthorized);
            }

            // 4. 取得全角色清單與轉生配點資訊（含最高跳樓層數 jumpMaxFloor 與轉生配點 reincarnation）
            var charactersRes = await _httpService.GetAsync<CharactersResponse>(CharactersEndpoint, token, cancellationToken);
            if (cancellationToken.IsCancellationRequested)
                return UserValidationResult.Cancelled();

            // 同步寫入全域 UserContext
            UserContext.SetProfile(meRes.Data, activeRes.Data.Character, statusRes.Data, charactersRes.Data);

            return UserValidationResult.Success(meRes.Data, activeRes.Data.Character, statusRes.Data);
        }

        /// <summary>
        /// 執行領取樓層獎勵（POST /api/action/floor-reward）。
        /// 領取成功時自動更新 UserContext 快取與下次冷卻時間。
        /// </summary>
        public async Task<FloorRewardClaimResult> ClaimFloorRewardAsync(string token, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(token))
                return FloorRewardClaimResult.Fail("Token 未設定，請輸入 Bearer Token。");

            var response = await _httpService.PostAsync<FloorRewardClaimResponse>(
                FloorRewardEndpoint,
                token,
                new { },
                cancellationToken);

            if (response.IsCancelled)
                return FloorRewardClaimResult.Cancelled();

            if (!response.IsSuccess || response.Data == null)
            {
                return FloorRewardClaimResult.Fail(response.ErrorMessage ?? "未知錯誤", response.IsUnauthorized);
            }

            // 成功領取，即時同步至 UserContext
            UserContext.UpdateFloorReward(response.Data);

            return FloorRewardClaimResult.Success(response.Data);
        }

        /// <summary>
        /// 查詢玩家角色清單與本次轉生配點資訊（GET /api/characters），並更新 UserContext 快取。
        /// </summary>
        public async Task<ApiResponse<CharactersResponse>> GetCharactersAsync(string token, CancellationToken cancellationToken = default)
        {
            var res = await _httpService.GetAsync<CharactersResponse>(CharactersEndpoint, token, cancellationToken);
            if (res.IsSuccess && res.Data != null)
            {
                UserContext.UpdateCharactersData(res.Data);
            }
            return res;
        }

        public void Dispose()
        {
            if (_ownsHttpService)
            {
                _httpService.Dispose();
            }
            GC.SuppressFinalize(this);
        }
    }

    /// <summary>
    /// 樓層獎勵領取結果封裝
    /// </summary>
    public class FloorRewardClaimResult
    {
        public bool IsSuccess { get; private set; }
        public bool IsCancelled { get; private set; }
        public bool IsUnauthorized { get; private set; }
        public string? ErrorMessage { get; private set; }
        public FloorRewardClaimResponse? Data { get; private set; }

        private FloorRewardClaimResult() { }

        public static FloorRewardClaimResult Success(FloorRewardClaimResponse data) =>
            new() { IsSuccess = true, Data = data };

        public static FloorRewardClaimResult Fail(string errorMessage, bool isUnauthorized = false) =>
            new() { IsSuccess = false, ErrorMessage = errorMessage, IsUnauthorized = isUnauthorized };

        public static FloorRewardClaimResult Cancelled() =>
            new() { IsCancelled = true };
    }

    /// <summary>
    /// 使用者 Token 驗證與資料拉取結果
    /// </summary>
    public class UserValidationResult
    {
        public bool IsSuccess { get; private set; }
        public bool IsCancelled { get; private set; }
        public bool IsUnauthorized { get; private set; }
        public string? ErrorMessage { get; private set; }
        public UserMeResponse? UserMe { get; private set; }
        public ActiveCharacter? ActiveCharacter { get; private set; }
        public ActionStatusResponse? ActionStatus { get; private set; }

        private UserValidationResult() { }

        public static UserValidationResult Success(UserMeResponse me, ActiveCharacter? activeChar, ActionStatusResponse? status) =>
            new()
            {
                IsSuccess = true,
                UserMe = me,
                ActiveCharacter = activeChar,
                ActionStatus = status
            };

        public static UserValidationResult Fail(string errorMessage, bool isUnauthorized = false) =>
            new()
            {
                IsSuccess = false,
                ErrorMessage = errorMessage,
                IsUnauthorized = isUnauthorized
            };

        public static UserValidationResult Cancelled() =>
            new() { IsCancelled = true };
    }
}
