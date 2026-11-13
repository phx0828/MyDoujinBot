using System;
using System.Collections.Generic;
using System.Linq;
using MyDoujinBot.Models;

namespace MyDoujinBot.Services
{
    /// <summary>
    /// 將 API 角色（CharacterEntry.Name + Title）對應到事件庫角色線（chain.name + chain.sub）。
    /// 比對規則：
    /// 1. 優先比對 Name（或「Name（...）」）且 Title 與 chain.sub（支援「／」拆分）一致者（處理「孤鳥」與「孤鳥（其他型態）」等同名多型態角色）。
    /// 2. 名稱完全相同（chain.name == Name）。
    /// 3. 事件庫名稱以「／」合併多名時，任一部分與 Name 相同（例如「大衛·馬丁內斯／露西娜·櫛名田」、「格林／愛麗絲」）。
    /// 對不上的角色回傳 null（UI 標示「無事件資料」）。
    /// </summary>
    public static class CharacterChainMatcher
    {
        public static ChainStatCounts? Match(CharacterEntry character, IEnumerable<ChainStatCounts> chains) =>
            Match(character.Name, character.Title, chains);

        public static ChainStatCounts? Match(string characterName, string? characterTitle, IEnumerable<ChainStatCounts> chains)
        {
            string name = Normalize(characterName);
            if (name.Length == 0) return null;

            string title = Normalize(characterTitle);
            var list = chains as IList<ChainStatCounts> ?? chains.ToList();

            // 1. 先以 Name + Title 比對 chain.Name / chain.Sub（可區分「孤鳥」大劍輕弩槍 vs 太刀狩獵笛／雙劍大鎚）
            if (title.Length > 0)
            {
                var byNameAndTitle = list.FirstOrDefault(c =>
                {
                    string cName = Normalize(c.ChainName);
                    bool nameMatches = cName == name ||
                                       cName.StartsWith(name + "（", StringComparison.Ordinal) ||
                                       cName.StartsWith(name + "(", StringComparison.Ordinal);
                    if (!nameMatches) return false;

                    return c.ChainSub
                        .Split('／', '/')
                        .Any(part => Normalize(part) == title);
                });
                if (byNameAndTitle != null) return byNameAndTitle;
            }

            // 2. 名稱完全一致
            var exact = list.FirstOrDefault(c => Normalize(c.ChainName) == name);
            if (exact != null) return exact;

            // 3. 合併角色線（以「／」或「/」分隔）
            return list.FirstOrDefault(c =>
                c.ChainName.Split('／', '/').Any(part => Normalize(part) == name));
        }

        private static string Normalize(string? s) => string.IsNullOrWhiteSpace(s) ? string.Empty : s.Trim();
    }
}
