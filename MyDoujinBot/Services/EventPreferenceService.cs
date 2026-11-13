using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MyDoujinBot.Models;

namespace MyDoujinBot.Services
{
    /// <summary>
    /// 事件偏好 API 服務（PUT /api/characters/event-preferences）。
    /// 讀取清單沿用 UserService.GetCharactersAsync（GET /api/characters）。
    /// </summary>
    public class EventPreferenceService : IDisposable
    {
        private const string Endpoint = $"{HttpService.ApiBaseUrl}/characters/event-preferences";
        private readonly HttpService _httpService;
        private readonly bool _ownsHttpService;

        public EventPreferenceService(HttpService? httpService = null)
        {
            _ownsHttpService = httpService == null;
            _httpService = httpService ?? new HttpService();
        }

        /// <summary>
        /// 將降低事件機率的角色 ID 清單送出到伺服器。成功後以伺服器回傳的 deprioritizedCharacterIds 同步更新 UserContext 快取。
        /// </summary>
        public async Task<ApiResponse<EventPreferencesPayload>> UpdateAsync(
            string token,
            IEnumerable<string> deprioritizedCharacterIds,
            CancellationToken cancellationToken = default)
        {
            var ids = deprioritizedCharacterIds
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Distinct()
                .ToList();

            var res = await _httpService.PutAsync<EventPreferencesPayload>(
                Endpoint,
                token,
                new EventPreferencesPayload { DeprioritizedCharacterIds = ids },
                cancellationToken);

            if (res.IsSuccess)
            {
                var data = UserContext.Current?.CharactersData;
                if (data != null)
                {
                    data.DeprioritizedCharacterIds = res.Data?.DeprioritizedCharacterIds ?? ids;
                }
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
}
