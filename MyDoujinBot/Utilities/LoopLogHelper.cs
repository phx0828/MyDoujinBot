using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using MyDoujinBot.Models;
using MyDoujinBot.Services;

namespace MyDoujinBot.Utilities
{
    /// <summary>
    /// 統一管理自動訓練、自動戰鬥、行動排程的日誌格式化與色彩標準。
    /// 確保三個執行迴圈輸出風格、文字、排版與色彩完全一致。
    /// </summary>
    public static class LoopLogHelper
    {
        // ── 統一日誌色彩常數 ──
        public static readonly Color SuccessGreen = Color.FromArgb(80, 200, 120);
        public static readonly Color WarningOrange = Color.FromArgb(220, 160, 80);
        public static readonly Color ErrorRed = Color.FromArgb(220, 80, 80);
        public static readonly Color InfoPurple = Color.FromArgb(200, 160, 255);
        public static readonly Color CooldownGray = Color.FromArgb(140, 140, 160);
        public static readonly Color HighlightBlue = Color.FromArgb(140, 200, 255);
        public static readonly Color StageCompletedGreen = Color.FromArgb(80, 220, 120);
        public static readonly Color LoopCompletedBlue = Color.FromArgb(180, 220, 255);
        public static readonly Color ChadoBrown = Color.FromArgb(200, 155, 106);
        public static readonly Color CharacterGold = Color.Gold;

        // =====================================================================
        // 訓練結果格式化
        // =====================================================================
        public static (string Message, Color Color) FormatTrainingResult(ActionResult ar)
        {
            var msg = $"[成功] 訓練 {ar.ActionName}：獲得 EXP {ar.Exp}";

            if (ar.LevelUps > 0)
                msg += $"，升級 +{ar.LevelUps}，目前等級 {ar.Level}";

            if (ar.Gains != null)
            {
                var nonZeroGains = new List<string>();
                foreach (var kv in ar.Gains)
                {
                    if (kv.Value != 0)
                    {
                        var statName = StatHelper.GetDisplayName(kv.Key);
                        nonZeroGains.Add($"{statName} +{kv.Value}");
                    }
                }

                if (nonZeroGains.Count > 0)
                    msg += $"，獲得能力：{string.Join("、", nonZeroGains)}";
            }

            return (msg, SuccessGreen);
        }

        // =====================================================================
        // 戰鬥結果格式化
        // =====================================================================
        public static (string Message, Color Color) FormatBattleResult(BattleType type, bool isWin, int expGained)
        {
            string typeStr = type == BattleType.Challenge ? "友好切磋" : "我要茶渡你";
            string winStr = isWin ? "勝利" : "失敗";
            Color logColor = isWin ? SuccessGreen : WarningOrange;
            return ($"[{winStr}] {typeStr}：獲得 EXP {expGained}", logColor);
        }

        public static (string Message, Color Color) FormatChadoSuccess()
        {
            return ("茶渡潑屎成功！", ChadoBrown);
        }

        // =====================================================================
        // 事件相關格式化
        // =====================================================================
        public static (string Message, Color Color) FormatEventSelection(PendingEvent pe, string? chosenOptionId)
        {
            var chosenOption = pe.Options?.Find(o => o.Id == chosenOptionId);
            var chanceTxt = chosenOption?.SuccessChance.HasValue == true
                ? $"（成功率 {chosenOption.SuccessChance}%）"
                : "（無判定）";
            return ($"[事件] {pe.Name} → 自動選擇「{chosenOption?.Name}」{chanceTxt}", InfoPurple);
        }

        public static (string Message, Color Color) FormatManualEventFallback(PendingEvent pe)
        {
            return ($"[事件] {pe.Name} — 人工選擇未配置，自動切換為自動選擇（最高成功率）。", InfoPurple);
        }

        public static (string Message, Color Color) FormatEventResult(EventResult er)
        {
            string prefix = er.IsSuccess ? "[事件成功]" : "[事件失敗]";
            Color color = er.IsSuccess ? SuccessGreen : ErrorRed;

            var msg = prefix;
            var bonusParts = BuildBonusStatsParts(er.Rewards?.BonusStats);
            if (bonusParts.Count > 0)
                msg += $" 獲得 {string.Join("、", bonusParts)}";

            return (msg, color);
        }

        public static (string Message, Color Color) FormatGainCharacter(string characterName)
        {
            return ($"[事件] 獲得角色：{characterName}", CharacterGold);
        }

        public static (string Message, Color Color) FormatEventApiError(string? errorMessage)
        {
            return ($"[事件失敗] API 錯誤：{errorMessage ?? "未知錯誤"}", ErrorRed);
        }

        // =====================================================================
        // 冷卻等待格式化
        // =====================================================================
        public static (string Message, Color Color) FormatCooldown(int cooldownMs, int actualDelayMs)
        {
            if (actualDelayMs > cooldownMs)
            {
                return ($"冷卻等待：{actualDelayMs / 1000.0:F1} 秒（基礎 {cooldownMs / 1000.0:F0}s + 額外隨機）", CooldownGray);
            }
            return ($"冷卻等待：{actualDelayMs / 1000.0:F1} 秒", CooldownGray);
        }

        // =====================================================================
        // 錯誤與 Token 格式化
        // =====================================================================
        public static (string Message, Color Color) FormatError(string? errorMessage)
        {
            return ($"[失敗] {errorMessage ?? "未知錯誤"}", ErrorRed);
        }

        public static (string Message, Color Color) FormatTokenExpired(string loopTypeName)
        {
            return ($"[錯誤] Token 無效或已過期，{loopTypeName}終止。請點擊「設定 Token」重新輸入。", ErrorRed);
        }

        // =====================================================================
        // 行動排程專用階段日誌
        // =====================================================================
        public static (string Message, Color Color) FormatStageStart(int currentStage, int totalStages, PipelineStage stage)
        {
            return ($"[排程] 進入第 {currentStage}/{totalStages} 階段「{stage.Name}」：{stage.GetActionSummary()}，目標：{stage.GetConditionSummary()}", HighlightBlue);
        }

        public static (string Message, Color Color) FormatStageConditionMet(int currentStage, int totalStages, PipelineStage stage, PipelineStage? nextStage)
        {
            if (nextStage != null)
            {
                return ($"[排程] ✓ 第 {currentStage} 階段「{stage.Name}」已達成條件（{stage.GetConditionSummary()}），切換至下一階段「{nextStage.Name}」...", StageCompletedGreen);
            }
            return ($"[排程] ✓ 第 {currentStage} 階段「{stage.Name}」已達成條件（{stage.GetConditionSummary()}）！", StageCompletedGreen);
        }

        public static (string Message, Color Color) FormatLoopCompleted(string message)
        {
            return (message, LoopCompletedBlue);
        }

        // =====================================================================
        // 共用輔助計算邏輯
        // =====================================================================
        public static List<string> BuildBonusStatsParts(Dictionary<string, int>? bonusStats)
        {
            var parts = new List<string>();
            if (bonusStats == null || bonusStats.Count == 0) return parts;

            foreach (var kv in bonusStats)
            {
                if (kv.Value != 0)
                {
                    var statName = StatHelper.GetDisplayName(kv.Key);
                    parts.Add($"{statName} +{kv.Value}");
                }
            }
            return parts;
        }

        public static string? AutoSelectOption(PendingEvent pe, AutoEventStrategy strategy)
        {
            if (pe.Options == null || pe.Options.Count == 0)
                return null;

            if (strategy == AutoEventStrategy.Random)
            {
                var idx = Random.Shared.Next(pe.Options.Count);
                return pe.Options[idx].Id;
            }

            var optionsWithChance = pe.Options
                .Where(o => o.SuccessChance.HasValue)
                .ToList();

            if (optionsWithChance.Count > 0)
            {
                var best = optionsWithChance.MaxBy(o => o.SuccessChance!.Value);
                return best?.Id;
            }

            var randomIndex = Random.Shared.Next(pe.Options.Count);
            return pe.Options[randomIndex].Id;
        }

        public static int CalculateActualDelay(int cooldownMs, double extraDelaySeconds)
        {
            if (extraDelaySeconds <= 0)
            {
                var minRandom = Random.Shared.Next(100, 500);
                return cooldownMs + minRandom;
            }

            var extraDelayMs = (int)(extraDelaySeconds * 1000);
            var randomMs = (int)(Random.Shared.NextDouble() * extraDelayMs) + 1;
            return cooldownMs + randomMs;
        }
    }
}
