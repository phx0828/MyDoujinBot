using System;
using System.Threading;
using System.Threading.Tasks;
using MyDoujinBot.Models;

namespace MyDoujinBot.Services
{
    public class BattleService
    {
        private const string BaseUrl = "https://mydoujin-backend.onrender.com/api";
        private readonly HttpService _httpService;

        public BattleService()
        {
            _httpService = new HttpService();
        }

        public async Task<ApiResponse<PlayerProfileResponse>> GetPlayerProfileAsync(string id, string token, CancellationToken cancellationToken = default)
        {
            string url = $"{BaseUrl}/players/{id}";
            return await _httpService.GetAsync<PlayerProfileResponse>(url, token, cancellationToken);
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
    }
}
