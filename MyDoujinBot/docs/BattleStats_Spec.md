# 戰鬥數據功能 需求規格 (Battle Stats Spec)

> 狀態：已實作完成。包含動態前綴辨識、BUFF 四階層歸屬判定、倒戈叛變行為統計、召喚次數、未歸屬效果區塊與雙陣營圖表呈現之完整規格。
> 原則：沿用現有專案風格與分層 (Models / Services / Forms / Utilities)，不另起新結構，客觀記錄規格。

## 1. 任務

玩家輸入戰報編號 → 呼叫 API 取得戰報 → 統計每位玩家與敵方/BOSS 的 輸出 / 承傷 / 治療 / 出手 / 爆擊 / 召喚 / 倒戈 → 以長條圖顯示。

## 2. 輸入：API

- `GET https://mydoujin-backend.onrender.com/api/battle-reports/{戰報編號}`（例：`.../118253`）
- 錯誤處理（需明確提示玩家）：編號不存在、網路失敗、逾時、回傳格式異常。

### 回傳資料結構

**基本資訊**：`reportId`、`battleType`（例 BOSS、PVP）、`boss`、`result`（WIN/LOSE）、`createdAt`

**participants.players**
- `userId`（唯一識別，顯示與讀 LOG 一律以此辨識）、`name`、`characterName`
- `characterId`、`title`、`level`、`stats` (hp, atk, def, sta, agi, spd, tec, int, luk)、`imagePath`

**participants.enemies**
- `entityId`（唯一識別，例 `mob_0_three_brothers_liu_bei`）
- `monsterId`、`name`、`title`、`floor`、`level`、`stats`、`imagePath`
- 召喚物也可能在此清單，帶 `summonerId`（召喚者 userId），`stats` 可能為 null。

**outcomes**
- `userId`、`name`（此處為角色名稱）、`expGained`、`newFloor`、`isDead`
- 可能缺少某位玩家，缺少時 `isDead` 視為 false，不可報錯。

### 資料處理規則
- 玩家清單以 `participants.players` 為準；頂層 `userIds` 不可作為玩家清單。
- 完整保存 players 與 enemies，以 `userId` / `entityId` 建立對照表。
- LOG 中的 ID 一律透過對照表轉換為名稱，不比對名稱字串。

## 3. 輸入：戰鬥 LOG

### 基本規則
- LOG 在頂層 `logs` 陣列，依序逐筆處理，以 `type` 區分事件。
- 一律使用結構化欄位為主，特定無施法者之狀態效果則輔以狀態歸屬機制。
- 傷害用 `actualDamage`（已扣除溢出），不用 `value`；只有 COUNTER、LUCK_EVENT、BUFF_EFFECT 沒有 `actualDamage`，才用 `value`。
- 玩家與敵方都累計，畫面顯示玩家區塊與敵方/BOSS區塊。
- 遇到不在 participants 內的 ID（如 `summon_1`），建立臨時單位，不中斷。

### 召喚物歸屬
- 召喚者對照表：處理 SUMMON 時記錄 `targetId`（召喚物）→ `actorId`（召喚者），並以 enemies 的 `summonerId` 補充。
- 召喚事件累計施放者之召喚次數（`Summons + 1`），**不計入出手次數**。
- 召喚物「主動造成」的傷害（DAMAGE、BLOCK、COUNTER 中 actor 為召喚物）：輸出、出手、爆擊都算召喚者。
- 僅限上述攻擊；召喚物承傷、召喚物治療不給召喚者。
- 玩家攻擊召喚物時，輸出仍算攻擊的玩家。

### 倒戈與敵友判定（Defection & Betrayal）
- 陣營判定：若攻擊方與受害方皆為我方玩家（同陣營），或治療方與受療方為敵對陣營（玩家補 BOSS / 敵方），皆視為倒戈叛變行為。
- 同隊傷害：攻擊方不累計至正常「輸出」，獨立累計至 `FriendlyDamageDealt`；受害方正常計入「承傷」，並記錄 `FriendlyDamageTaken`。
- 對同隊出手：跨陣營攻擊計入「出手次數」，對同隊出手則獨立累計至 `FriendlyShots`，不計入正常「出手次數」。
- 治療敵方：治療方不累計至正常「治療」，獨立累計至 `EnemyHealDealt`，次數計入 `EnemyHealCount`。

### BUFF_EFFECT 四階層歸屬機制 (Four-Tier Buff Attribution)
針對長時間持續性 DOT（傷害）與 HOT（治療）之 BUFF_EFFECT 事件，依以下四階層精確追溯施放者：
1. **Tier 1: 即時記憶對照 (Active Buff Mapping)**
   - 以特定受體（targetId）為單位，監聽 `BUFF_APPLY` 與 `SKILL_TEXT` 事件。當技能附加狀態（如 `buffId`、`skillId`、狀態名稱）至特定目標時，於對照表中記錄 `(targetId, buffKey) -> [casterIds]`。
   - 若隊伍中有同角色陸續對該目標施放同一 BUFF，依序追加至施放者清單。
   - 後續 `BUFF_EFFECT` 觸發時優先比對此映射。若清單內有多位施放者，將傷害或治療數值平均分配給所有施放者（除以施放人數），數值保留至小數點一位。
2. **Tier 2: 動態前綴與精確技能解析 (Dynamic Prefix & Skill Mapping)**
   - 無需寫死特定角色名稱。初始化時自動以出戰敵方之 `monsterId`、`entityId` 等建立敵方技能前綴；同時自整場戰鬥之 `SKILL_TEXT` / `DAMAGE` 事件中，動態學習技能 ID 與施放者的精準對應表（例如「同人」借用「西米」技能施放，可直接精確解析回同人）。
   - 若 `buffId` 帶有前綴（如 `bocchi_`、`sakura_`），比對當前出戰角色或敵方之技能字首識別施放者。
3. **Tier 3: 狀態事件全場回溯 (Timeline Backtracking)**
   - 若前兩層未命中，自訊息提取 BUFF 名稱，自當前索引 `currentIndex - 1` 回溯至戰鬥開端（索引 0）。
   - 比對包含該 BUFF 名稱或 ID、且目標一致的前置施放事件（需排除 `BUFF_EFFECT` 本身），找到所有在此時間點前施放過的施放者，並回填至 Active Map。
4. **Tier 4: 未知/無法歸屬效果 (Unattributed Buff Fallback)**
   - 受害者依舊正常承傷（`target.Taken += value`）。
   - 若前三層皆無法判定來源，輸出與治療不計入任何單一角色，改依受體陣營記錄為隊伍層級的未歸類傷害或治療：
     - 受體為敵方：記錄至「我方 對 敵方 造成未歸類傷害 / 治療」（呈現於我方隊伍圖表下方，無分隔線）。
     - 受體為我方：記錄至「敵方 對 我方 造成未歸類傷害 / 治療」（呈現於敵方/BOSS圖表下方，無分隔線）。

### 單位統計欄位
輸出、承傷、治療、出手次數、命中次數、爆擊次數、召喚次數、同隊傷害、受到友傷、治療敵方、對同隊出手、對敵方治療；命中 = 出手 − MISS；命中率 = 命中 ÷ 出手；爆擊率 = 爆擊 ÷ 命中（分母為 0 時為 0.0%）。

### PVP 戰報
- `battleType == "PVP"`；對手同樣放在 `participants.enemies`，但沒有 `entityId`，改以 `userId` 識別，並帶有 `characterName`。
- 敵方單位一律以 `entityId ?? userId` 作為唯一識別（`BattleReportEnemy.Id`）。
- 統計規則與 PVE 相同；圖表下方區塊標題改為【對手統計】，標題行格式同玩家：`玩家名稱 (角色名稱) 【倒下】`。
- `CHADO`（茶渡結尾嘲諷）為已辨識、不影響統計的事件。

### 事件處理規則

| type | 規則 |
|---|---|
| DAMAGE | 略過 `isCounter == true`（與前一筆 COUNTER 重複）。actor 輸出 +actualDamage；target 承傷 +actualDamage。同陣營時依倒戈規則轉移。**有 `isCrit` 欄位**才算一次出手（actor 出手 +1，`isCrit==true` 爆擊 +1）。**無 `isCrit`** 只累傷害不算出手：source=REFLECTION、source=DIRECT、skillId=`three_brothers_guard` |
| BLOCK | 同 DAMAGE：actor 輸出、target 承傷，用 actualDamage。同陣營時依倒戈規則轉移。每筆都算一次出手（isCrit 則爆擊 +1）。`actualDamage==0` 是被替身擋下，後接一筆 three_brothers_guard DAMAGE，傷害由該筆累計，BLOCK 只算出手。方向不可相反（actor 攻擊方、target 被攻擊方） |
| MISS | actor 為攻擊方、target 為閃避方。跨陣營：actor 出手 +1，不算命中、不爆擊、不累計傷害。同陣營：actor 對同隊出手 +1 |
| COUNTER | actor 輸出 +value；target 承傷 +value。不算出手、不算爆擊 |
| LUCK_EVENT | actor 承傷 +value；`opponentId` 輸出 +value。不算出手 |
| HEAL | actor 治療 +value（算施放者）；若受治者為敵方，累計至 EnemyHealDealt 與 EnemyHealCount，不計入正常 Heal |
| BUFF_APPLY | 註冊主動 BUFF 施放關係（Tier 1） |
| SKILL_TEXT | 捕捉技能文本中之狀態施加事件（Tier 1）與動態技能擁有者（Tier 2） |
| BUFF_EFFECT | 經四階層解析施放者。`buffType=HOT`：施放者治療 +value（敵方則計為 EnemyHeal）。`buffType=DOT`：受害者承傷 +value，施放者輸出 +value（同隊則計為 FriendlyDamage）。無法判定者記至 UnattributedBuffs |
| DEATH | 用來判斷單位「倒下」狀態 |
| SUMMON | 建立召喚物對照表，累計 actor 召喚次數 `Summons + 1`，不計入出手次數 |

**已辨識、不影響統計（略過，不計入「無法辨識」）**：
BATTLE_START、BOSS_DIALOGUE、TEXT、SP_RECOVER（回復 SP，若文字含 HP 則計入治療）、HP_REMAINING、EXP_GAIN、SANITY_CHANGE、SANITY_STATE_CHANGE、FATIGUE、BRICK_CRACK、PLAYER_DEATH、PLAYER_DEFEAT（死亡狀態改由 `outcomes.isDead` 判斷）、PART_BREAK、REST、CHADO

### 解析要求
type 不在上述清單、或缺少必要欄位的資料：略過並計數，結束時回報筆數。

## 4. 統計規則
- 以單位（userId/entityId）累加 輸出、承傷、治療、出手、爆擊、召喚、倒戈數據。
- 畫面顯示 `participants.players` 與敵方/BOSS。
- 出手、爆擊、爆擊率、召喚、倒戈指標需計算並顯示。

## 5. 輸出格式

每位玩家：標題行 → 輸出 / 承傷 / 治療 三行長條 → 一行出手與爆擊資訊（含召喚次數） → 若有倒戈行為則顯示兩行警告。

### 戰報標題行
格式：`戰報 #{reportId}｜{battleType}｜{floorNumber}F｜{bossName}｜{result}`（若無層數則不顯示層數，如 PVP：`戰報 #{reportId}｜PVP｜{opponentName}｜{result}`）。
例：`戰報 #270110｜BOSS｜46F｜高士官長｜失敗`

### 玩家標題行
格式：`玩家名稱 (角色名稱) 【倒下】 【死亡】`
- 玩家名稱 = `players.name`；角色名稱 = `players.characterName`
- 【倒下】：logs 中存在 `type==DEATH` 且 `actorId==該玩家 userId`
- 【死亡】：`outcomes` 中該玩家 `isDead==true`（缺資料視為 false）
- 兩標記獨立判斷、可同時出現；都不符合則不顯示任何標記，**不留多餘空格**
- 【死亡】= 紅色粗體；【倒下】= 橘色粗體；`玩家名稱 (角色名稱)` = 水藍色粗體（`ChartStyle.UnitTitle`，RGB: 100, 210, 255）
- 敵方單位標題行同樣套用水藍色粗體樣式

### 出手與爆擊資訊
- 三條長條之後另起一行，純文字、不畫長條、預設顏色。
- 格式：`出手 N 次｜命中 N 次｜命中率 N.N%｜爆擊 N 次｜爆擊率 N.N%`（若有發動過 SUMMON 則行末加上`｜召喚 N 次`）。
- 出手次數不包含 SUMMON；召喚物攻擊算召喚者；反擊不計入。
- 命中 = 出手 − MISS（被完全擋下的 BLOCK 仍算命中）。
- 命中率 = 命中 ÷ 出手 × 100；爆擊率 = 爆擊 ÷ 命中 × 100；皆四捨五入到小數 1 位。分母為 0 顯示 `0.0%`。

### 倒戈叛變行為行
若玩家具備倒戈叛變數值（`FriendlyDamageDealt > 0` 或 `EnemyHealDealt > 0` 或 `FriendlyShots > 0` 或 `EnemyHealCount > 0`）：
- 第一行：`⚠️ 同隊傷害 5,306｜治療敵方 21,504`
- 第二行：`⚠️ 對同隊出手 13 次｜對敵方治療 4 次`
- 樣式：兩行皆使用橘黃色警告樣式（`ChartStyle.Warning`，RGB: 255, 170, 70）。

### 未歸類傷害 / 治療區塊
若戰報中存在 Tier 4 無法歸屬施放者的 BUFF_EFFECT（如環境毒/火、無前置施放之被動）：
- **我方 對 敵方** 之未歸類效果：緊接於我方玩家圖表下方呈現（與隊伍圖表之間無分隔線）。
- **敵方 對 我方** 之未歸類效果：緊接於敵方 / BOSS 圖表下方呈現（無分隔線）。
- 格式：
  - 標題行：`[未歸類傷害] `（水藍色粗體 `ChartStyle.UnitTitle`，RGB: 100, 210, 255）＋ `系統無法辨識效果來源`（灰斜體 `ChartStyle.Muted`）
  - 內容行：`我方 對 敵方 造成未歸類傷害：{效果與數值}` 或 `敵方 對 我方 造成未歸類傷害：{效果與數值}`（一般字體 `ChartStyle.Normal`）
例：
```
[未歸類傷害] 系統無法辨識效果來源
敵方 對 我方 造成未歸類傷害：中毒 3,153 點、灼傷 444 點
```

### 範例
```
戰報 #270110｜BOSS｜46F｜高士官長｜失敗

老ㄐ訶德 (寶月夜宵)
輸出 █████████ 56,433
承傷 █ 8,367
治療 0
出手 36 次｜命中 36 次｜命中率 100.0%｜爆擊 10 次｜爆擊率 27.8%｜召喚 4 次
⚠️ 同隊傷害 16,676｜治療敵方 0
⚠️ 對同隊出手 14 次｜對敵方治療 0 次

阿爾吉利亞 (同人) 【死亡】
輸出 █ 7,541
承傷 ██ 14,958
治療 ████ 23,583
出手 13 次｜命中 6 次｜命中率 46.2%｜爆擊 0 次｜爆擊率 0.0%
⚠️ 同隊傷害 5,306｜治療敵方 21,504
⚠️ 對同隊出手 13 次｜對敵方治療 4 次

Bulleyez (木之本櫻) 【死亡】
輸出 ██████████ 60,097
承傷 ██ 13,092
治療 0
出手 42 次｜命中 33 次｜命中率 78.6%｜爆擊 14 次｜爆擊率 42.4%
⚠️ 同隊傷害 5,867｜治療敵方 0
⚠️ 對同隊出手 15 次｜對敵方治療 0 次

────────────────────────────────────────
【敵方 / BOSS 統計】

高士官長 (人才招募員)
輸出 ██ 13,909
承傷 ████████████████████ 124,071
治療 0
出手 14 次｜命中 10 次｜命中率 71.4%｜爆擊 5 次｜爆擊率 50.0%
```

### 圖表規格
- 顏色：輸出=紅、承傷=藍、治療=綠（長條與對應顏色一致）；警告=橘黃；標題=金黃；註解=灰斜
- 長條長度：全場統一最大值基準（Global Scale），涵蓋玩家與敵方最大數值為 100% 等比例縮放
- 數值千分位逗號；非零數值至少 1 格

## 6. 其他要求
- 取得資料、解析、統計、繪圖分成獨立函式/類別。
- 每種事件類型的處理獨立成各自函式或區塊，方便新增事件類型。
- 盡量結構化、解耦，降低維護成本。

## 7. 現有專案參考（實作時沿用）

- 網路層：[HttpService.cs](../Services/HttpService.cs) 的 `GetAsync<T>` / `ApiResponse<T>`。
- 業務層：[BattleService.cs](../Services/BattleService.cs)（`BaseUrl` 已為 `https://mydoujin-backend.onrender.com/api`）。
- Models：[Models/](../Models/)（`BattleReportResponse`、`BattleStatsResult` 等）。
- UI：[BattleStatsForm.cs](../Forms/BattleStatsForm.cs)（深色主題背景 `28,28,38`、RichTextBox LOG 區）。
