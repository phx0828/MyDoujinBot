using System;
using System.Collections.Generic;
using MyDoujinBot.Models;

namespace MyDoujinBot.Services
{
    /// <summary>
    /// 統計層：依序逐筆處理 logs，以 type 分派給對應的事件處理函式。
    ///
    /// 新增事件類型的步驟：
    /// 1. 在 BattleEventHandlers 新增 HandleXxx 函式。
    /// 2. 在下方 Handlers 處理表註冊 "XXX" → HandleXxx。
    /// 若該事件只是「已辨識、不影響統計」，把 type 加進 IgnoredTypes 即可。
    /// </summary>
    public static class BattleStatsCalculator
    {
        private delegate bool EventHandler(BattleStatsContext ctx, BattleReportLog log);

        /// <summary>type → 處理函式。</summary>
        private static readonly Dictionary<string, EventHandler> Handlers = new(StringComparer.Ordinal)
        {
            ["DAMAGE"] = BattleEventHandlers.HandleDamage,
            ["BLOCK"] = BattleEventHandlers.HandleBlock,
            ["MISS"] = BattleEventHandlers.HandleMiss,
            ["COUNTER"] = BattleEventHandlers.HandleCounter,
            ["LUCK_EVENT"] = BattleEventHandlers.HandleLuckEvent,
            ["HEAL"] = BattleEventHandlers.HandleHeal,
            ["BUFF_EFFECT"] = BattleEventHandlers.HandleBuffEffect,
            ["SP_RECOVER"] = BattleEventHandlers.HandleSpRecover,
            ["DEATH"] = BattleEventHandlers.HandleDeath,
            ["SUMMON"] = BattleEventHandlers.HandleSummon,
        };

        /// <summary>
        /// 已辨識、不影響統計的事件（略過，不計入「無法辨識」）。
        /// - PLAYER_DEATH / PLAYER_DEFEAT：死亡狀態改由 outcomes.isDead 判斷。
        /// - PART_BREAK：部位被破壞，傷害已在前一筆 DAMAGE 累計，value 不是傷害。
        /// - REST：回復的是體力（SP），不是 HP，不可算成治療。
        /// - CHADO：PVP「我要茶渡你」結尾的嘲諷文字，不影響數據。
        /// </summary>
        private static readonly HashSet<string> IgnoredTypes = new(StringComparer.Ordinal)
        {
            "BATTLE_START", "BOSS_DIALOGUE", "SKILL_TEXT", "TEXT", "BUFF_APPLY",
            "HP_REMAINING", "EXP_GAIN", "SANITY_CHANGE", "SANITY_STATE_CHANGE",
            "FATIGUE", "BRICK_CRACK", "PLAYER_DEATH", "PLAYER_DEFEAT",
            "PART_BREAK", "REST", "CHADO"
        };

        /// <summary>
        /// 統計整份戰報。type 不在清單、或缺少必要欄位的事件會略過並計數（SkippedCount / SkippedByType）。
        /// </summary>
        public static BattleStatsResult Calculate(BattleReportIndex index, IEnumerable<BattleReportLog?> logs)
        {
            var ctx = new BattleStatsContext(index);

            foreach (var log in logs)
            {
                if (log == null)
                {
                    RecordSkipped(ctx.Result, "(空白事件)");
                    continue;
                }

                var type = log.Type;
                if (string.IsNullOrWhiteSpace(type))
                {
                    RecordSkipped(ctx.Result, "(無 type)");
                    continue;
                }

                if (IgnoredTypes.Contains(type)) continue;

                if (!Handlers.TryGetValue(type, out var handler))
                {
                    RecordSkipped(ctx.Result, type); // 無法辨識的 type
                    continue;
                }

                if (!handler(ctx, log))
                    RecordSkipped(ctx.Result, type); // 缺少必要欄位
            }

            return ctx.Result;
        }

        private static void RecordSkipped(BattleStatsResult result, string type)
        {
            result.SkippedCount++;
            result.SkippedByType[type] = result.SkippedByType.TryGetValue(type, out var count) ? count + 1 : 1;
        }
    }
}
