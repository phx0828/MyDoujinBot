using System;
using MyDoujinBot.Models;

namespace MyDoujinBot.Services
{
    /// <summary>
    /// 全域使用者資訊與狀態上下文。
    /// 集中儲存目前已驗證玩家的基本資料 (users/me)、當前出戰角色詳情 (characters/active)
    /// 與系統冷卻狀態 (action/status)，供全專案任何需要存取的地方即時取得。
    /// </summary>
    public class UserContext
    {
        private static readonly object _lock = new();

        /// <summary>目前全域上下文實例</summary>
        public static UserContext Current { get; private set; } = new();

        /// <summary>玩家基本資訊（對應 GET /api/users/me）</summary>
        public UserMeResponse? UserMe { get; private set; }

        /// <summary>當前出戰角色詳情（對應 GET /api/characters/active 內之 character）</summary>
        public ActiveCharacter? ActiveCharacter { get; private set; }

        /// <summary>行動與即時狀態（對應 GET /api/action/status）</summary>
        public ActionStatusResponse? ActionStatus { get; private set; }

        /// <summary>使用者資料是否已成功載入並完成驗證</summary>
        public bool IsAuthenticated => UserMe != null && ActiveCharacter != null;

        /// <summary>玩家公開顯示名稱（優先使用個人暱稱，無則使用帳號名稱）</summary>
        public string PlayerName
        {
            get
            {
                if (UserMe != null && !string.IsNullOrWhiteSpace(UserMe.Nickname))
                    return UserMe.Nickname;
                if (UserMe != null && !string.IsNullOrWhiteSpace(UserMe.Username))
                    return UserMe.Username;
                return "—";
            }
        }

        /// <summary>當前出戰角色等級</summary>
        public int Level => ActiveCharacter?.Level ?? 0;

        /// <summary>當前所在最高樓層</summary>
        public int CurrentFloor => UserMe?.CurrentFloor ?? 0;

        /// <summary>下次可領取樓層獎勵的預估時間點（UTC），若無資料或尚未載入則為 null</summary>
        public DateTimeOffset? NextFloorRewardClaimTime { get; private set; }

        /// <summary>
        /// 是否達到領取樓層獎勵條件。
        /// 條件：必須已成功取得樓層獎勵資料，且當前 UTC 時間已到達或超過下次可領取時間。
        /// </summary>
        public bool CanClaimFloorReward
        {
            get
            {
                if (ActionStatus?.FloorReward == null || !NextFloorRewardClaimTime.HasValue)
                    return false; // 無資料時絕對不可領取

                return DateTimeOffset.UtcNow >= NextFloorRewardClaimTime.Value;
            }
        }

        /// <summary>
        /// 樓層獎勵目前剩餘冷卻時間（毫秒）。
        /// 若尚未取得資料或未驗證則回傳 null；若已可領取則回傳 0。
        /// </summary>
        public long? FloorRewardCooldownRemainingMs
        {
            get
            {
                if (ActionStatus?.FloorReward == null || !NextFloorRewardClaimTime.HasValue)
                    return null;

                var diff = (long)(NextFloorRewardClaimTime.Value - DateTimeOffset.UtcNow).TotalMilliseconds;
                return diff > 0 ? diff : 0;
            }
        }

        /// <summary>
        /// 格式化摘要字串：玩家名 ｜ 等級 ｜ 目前樓層：41F
        /// 例："阿爾吉利亞 ｜ Lv.74 ｜ 目前樓層：41F"
        /// </summary>
        public string SummaryText
        {
            get
            {
                if (!IsAuthenticated)
                    return "— ｜ Lv.— ｜ 目前樓層：—F";

                var lvlStr = Level > 0 ? $"Lv.{Level}" : "Lv.—";
                var floorStr = CurrentFloor > 0 ? $"{CurrentFloor}F" : "—F";
                return $"{PlayerName} ｜ {lvlStr} ｜ 目前樓層：{floorStr}";
            }
        }

        /// <summary>當使用者資料發生更新時觸發之全域通知事件</summary>
        public static event Action? OnProfileUpdated;

        /// <summary>
        /// 更新並設定目前已驗證之使用者完整資料。
        /// </summary>
        public static void SetProfile(UserMeResponse me, ActiveCharacter? activeChar, ActionStatusResponse? status)
        {
            var nextClaim = (status?.FloorReward != null)
                ? DateTimeOffset.UtcNow.AddMilliseconds(Math.Max(0, status.FloorReward.CooldownRemainingMs))
                : (DateTimeOffset?)null;

            lock (_lock)
            {
                Current = new UserContext
                {
                    UserMe = me,
                    ActiveCharacter = activeChar,
                    ActionStatus = status,
                    NextFloorRewardClaimTime = nextClaim
                };
            }
            OnProfileUpdated?.Invoke();
        }

        /// <summary>
        /// 更新行動狀態（例如定時輪詢 action/status 或領取樓層獎勵後更新）
        /// </summary>
        public static void UpdateStatus(ActionStatusResponse? status)
        {
            if (status == null) return;
            var nextClaim = (status.FloorReward != null)
                ? DateTimeOffset.UtcNow.AddMilliseconds(Math.Max(0, status.FloorReward.CooldownRemainingMs))
                : (DateTimeOffset?)null;

            lock (_lock)
            {
                if (Current != null)
                {
                    Current.ActionStatus = status;
                    Current.NextFloorRewardClaimTime = nextClaim;
                }
            }
            OnProfileUpdated?.Invoke();
        }

        /// <summary>
        /// 即時更新出戰角色等級（例如訓練升級時調用）。
        /// </summary>
        public static void UpdateLevel(int newLevel)
        {
            if (newLevel <= 0) return;

            lock (_lock)
            {
                if (Current.ActiveCharacter != null)
                {
                    Current.ActiveCharacter.Level = newLevel;
                }
            }
            OnProfileUpdated?.Invoke();
        }

        /// <summary>
        /// 即時更新目前最高樓層。
        /// </summary>
        public static void UpdateFloor(int newFloor)
        {
            if (newFloor <= 0) return;

            lock (_lock)
            {
                if (Current.UserMe != null)
                {
                    Current.UserMe.CurrentFloor = newFloor;
                }
            }
            OnProfileUpdated?.Invoke();
        }

        /// <summary>
        /// 領取樓層獎勵成功後更新冷卻時間與相關角色數值。
        /// </summary>
        public static void UpdateFloorReward(FloorRewardClaimResponse response)
        {
            if (response == null) return;

            var nextClaim = DateTimeOffset.UtcNow.AddMilliseconds(Math.Max(0, response.CooldownRemainingMs));

            lock (_lock)
            {
                if (Current != null)
                {
                    Current.NextFloorRewardClaimTime = nextClaim;

                    if (Current.ActionStatus != null)
                    {
                        Current.ActionStatus.FloorReward ??= new FloorRewardInfo();
                        Current.ActionStatus.FloorReward.Floor = response.Floor;
                        Current.ActionStatus.FloorReward.Exp = (int)response.Exp;
                        Current.ActionStatus.FloorReward.CooldownMs = response.CooldownMs;
                        Current.ActionStatus.FloorReward.CooldownRemainingMs = response.CooldownRemainingMs;
                        Current.ActionStatus.FloorReward.ServerTime = response.ServerTime;
                        Current.ActionStatus.FloorReward.IsDead = response.IsDead;
                        Current.ActionStatus.FloorReward.IsMaxLevel = response.IsMaxLevel;
                    }

                    if (response.RewardResult != null && response.RewardResult.Level > 0 && Current.ActiveCharacter != null)
                    {
                        Current.ActiveCharacter.Level = response.RewardResult.Level;
                    }

                    if (response.Floor > 0 && Current.UserMe != null)
                    {
                        Current.UserMe.CurrentFloor = response.Floor;
                    }
                }
            }
            OnProfileUpdated?.Invoke();
        }

        /// <summary>
        /// 清除目前快取之使用者資料（例如 Token 失效或登出時）。
        /// </summary>
        public static void Clear()
        {
            lock (_lock)
            {
                Current = new UserContext();
            }
            OnProfileUpdated?.Invoke();
        }
    }
}
