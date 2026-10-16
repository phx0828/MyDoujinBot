# 遊戲延伸 API 規格文件 (Game APIs Spec)

> 狀態：**已確認端點與遊戲頁面欄位對齊**。
> 資料來源：取自真實擷取之 API Response JSON，並與遊戲介面之各項名詞完成 100% 精準對照。
> 通訊原則：Header 配置沿用既有 `HttpService`（含 Bearer Token、User-Agent、Referer、Origin）。除 `action/explore` 為 `POST` 外，其餘延伸查詢端點均為 `GET`。

---

## 1. 玩家自身基本資訊 (`users/me`)

- **端點 URL**：`GET https://mydoujin-backend.onrender.com/api/users/me`
- **用途**：取得目前登入使用者的帳號資訊、當前出戰角色 ID 與目前所在最高樓層。
- **對應 C# Model**：[UserMeResponse.cs](file:///d:/MyDoujinBot/MyDoujinBot/Models/UserMeResponse.cs)

### 欄位結構
| 欄位名稱 | 型別 | 遊戲介面對應名稱 | 說明 / 範例值 |
| :--- | :--- | :--- | :--- |
| `id` | `string` | 使用者 ID | 唯一識別碼 (UserId，與 Token 一致)，例：`"6aace3d256161900a8a0f4bf"` |
| `discordId` | `string` | Discord ID | 綁定之 Discord 使用者 ID，例：`"460854284087721984"` |
| `username` | `string` | 帳號名稱 | 登入帳號，例：`"phx0828"` |
| `nickname` | `string` | **暱稱** | 玩家個人公開暱稱，例：`"阿爾吉利亞"` |
| `personalStatus` | `string` | **個人狀態** | 個人個性簽名，例：`"Shringara Love：啊好痛苦但這才是愛，你也感受吧"` |
| `activeCharacterId`| `string` | 出戰角色 ID | 當前出戰角色之實例 ObjectId，例：`"6acf0df8734f55ff241d9943"` |
| `currentFloor` | `int` | 當前最高樓層 | 目前通關/所在最高樓層，例：`41` |

---

## 2. 行動與即時狀態 (`action/status`)

- **端點 URL**：`GET https://mydoujin-backend.onrender.com/api/action/status`
- **用途**：取得即時冷卻剩餘時間、未決策的突發事件、樓層獎勵冷卻與滿等狀態。
- **對應 C# Model**：[ActionStatusResponse.cs](file:///d:/MyDoujinBot/MyDoujinBot/Models/ActionStatusResponse.cs)

### 欄位結構
| 欄位名稱 | 型別 | 說明 / 作用 |
| :--- | :--- | :--- |
| `cooldownRemainingMs` | `int` | **重要：距離下次可行動的冷卻剩餘毫秒數**（如 `4536`，可用於啟動時精確同步冷卻） |
| `cooldownMs` | `int` | 基礎冷卻時間長度 (毫秒，例：`10000` = 10 秒) |
| `pendingEvent` | `PendingEvent?` | **重要：目前是否有卡住未處理的事件**（無為 `null`，有則為事件詳情與選項清單） |
| `isMaxLevel` | `bool` | 當前出戰角色是否已達等級上限 (例：`false`) |
| `lastActionAt` | `DateTimeOffset` | 上次執行行動之 UTC 時間 (例：`"2026-10-16T15:09:10.213Z"`) |
| `serverTime` | `DateTimeOffset` | 伺服器當前 UTC 時間 (例：`"2026-10-16T15:09:15.677Z"`) |
| `floorReward` | `FloorRewardInfo` | **樓層獎勵物件** (詳見下方子結構) |
| `trainingSpec` | `TrainingSpecInfo` | **委託/長時間訓練設定規格** (詳見下方子結構) |

#### `floorReward` 樓層獎勵子結構
| 欄位名稱 | 型別 | 說明 / 數值範例 |
| :--- | :--- | :--- |
| `floor` | `int` | 當前可領取獎勵之樓層 (例：`41`) |
| `exp` | `int` | **樓層獎勵給的經驗值** (例：`4100`) |
| `cooldownRemainingMs` | `long` | **樓層獎勵領取冷卻時間**（為 `0` 時代表已可領取，例：`11626884`） |
| `cooldownMs` | `long` | 獎勵結算週期 (毫秒，例：`14400000` = 4小時) |
| `serverTime` | `DateTimeOffset` | 伺服器時間 (例：`"2026-10-16T15:09:15.677Z"`) |
| `isDead` | `bool` | 角色當前是否死亡 (例：`false`) |
| `isMaxLevel` | `bool` | 角色當前是否滿等 (例：`false`) |

#### `trainingSpec` 訓練規格子結構
| 欄位名稱 | 型別 | 說明 / 數值範例 |
| :--- | :--- | :--- |
| `minMinutes` | `int` | 最短訓練時間分鐘數 (例：`30`) |
| `maxMinutes` | `int` | 最長訓練時間分鐘數 (例：`480` = 8小時) |
| `stepMinutes` | `int` | 調整步長分鐘數 (例：`30`) |

---

## 3. 當前出戰角色詳情 (`characters/active`)

- **端點 URL**：`GET https://mydoujin-backend.onrender.com/api/characters/active`
- **用途**：取得當前出戰角色的各項九圍能力、扔屎與沾屎戰績統計、技能配備、紙娃娃外觀與轉生狀態。
- **頂層包裹**：`{ "character": { ... } }`
- **對應 C# Model**：[ActiveCharacterResponse.cs](file:///d:/MyDoujinBot/MyDoujinBot/Models/ActiveCharacterResponse.cs)

### 基本資訊 (`character`)
| 欄位名稱 | 遊戲介面對應名稱 | 型別 | 說明 / 數值範例 |
| :--- | :--- | :--- | :--- |
| `_id` | 角色實例 ID | `string` | ObjectId，例：`"6acf0df8734f55ff241d9943"` |
| `userId` | 玩家帳號 ID | `string` | 擁有者 ObjectId，例：`"6aace3d256161900a8a0f4bf"` |
| `characterId` | 角色原型代號 | `string` | 原型代號，例：`"doujin"` |
| `formId` | 型態 ID | `string?` | 角色型態（未轉變時為 `null`） |
| `name` | **角色** | `string` | 角色名稱，例：`"同人"` |
| `title` | **稱號** | `string` | 角色頭銜，例：`"我的同人"` |
| `imagePath` | 角色立繪圖檔 | `string` | 立繪路徑，例：`"/images/characters/kirito.webp"` |
| `level` | **等級** | `int` | 目前等級，例：`74` |
| `exp` / `totalExp` | 經驗值 / 總經驗值 | `int` / `int` | 當前等級 EXP (`1560`) / 累計總 EXP (`96095`) |
| `reincarnationCount` | **轉生次數** | `int` | 轉生次數，例：`22` |
| `isDead` | 死亡標記 | `bool` | 是否處於死亡狀態 (例：`false`) |
| `isSoiled` | 沾屎標記 | `bool` | 是否處於髒污狀態 (例：`false`) |
| `normalAttackId` | 普攻技能 | `string` | 代號，例：`"standard_offensive_magic"` |
| `skillIds` | 配備技能 | `List<string>` | 主動技能 ID 清單 (`["minor_erdtree", "first_aid", "simmer_staff_meal"]`) |
| `doujinOwned` | 擁有同人標記 | `bool?` | 玩家自訂同人持有狀態 (例：`true`) |

### 子結構：外觀樣式 (`character.appearance`)
| 欄位名稱 | 型別 | 範例值 | 說明 |
| :--- | :--- | :--- | :--- |
| `frontHair` | `string` | `"#f7f5ad"` | 前髮顏色 HEX |
| `backHair` | `string` | `"#000091"` | 後髮顏色 HEX |
| `brows` | `string` | `"#5c3900"` | 眉毛顏色 HEX |
| `eyes` | `string` | `"#05ab3a"` | 眼睛顏色 HEX |
| `skin` | `string` | `"#edbb82"` | 膚色 HEX |
| `outfit` | `string` | `"#450000"` | 服裝顏色 HEX |
| `frontHairStyle` | `string` | `"crop"` | 前髮造型代號 |
| `backHairStyle` | `string` | `"short"` | 後髮造型代號 |
| `browStyle` | `string` | `"bold"` | 眉毛造型代號 |
| `eyeStyle` | `string` | `"almond"` | 眼型代號 |
| `outfitStyle` | `string` | `"hoodie"` | 服裝造型代號 |

### 九大戰鬥屬性 (`character.stats`)
| 欄位名稱 | 遊戲名稱 | 數值範例 | 遊戲內機制說明 |
| :--- | :--- | :--- | :--- |
| `hp` | **HP** | `8740` | 生命值，歸零即倒下。 |
| `atk` | **攻擊** | `167` | 影響普通攻擊與技能傷害。 |
| `def` | **防禦** | `336` | 減少受到的傷害。 |
| `sta` | **體力** | `178` | 影響持續作戰能力與部分技能發動。 |
| `agi` | **敏捷** | `175` | 影響迴避與攻擊命中。 |
| `spd` | **反應速度** | `158` | 決定先攻、連擊次數，以及額外回合。 |
| `tec` | **技巧** | `121` | 影響命中、爆擊傷害，以及反擊機率。 |
| `int` | **智力** | `549` | 影響法術傷害與治療量。 |
| `luk` | **幸運** | `103` | 最神奇的屬性，能在戰鬥中造成意想不到的效果。 |

### 戰績數據對應表 (`combatStats` vs `totalCombatStats`)
遊戲介面個人資訊完全對應以下兩組欄位（本輪 vs 生涯累計）：

| 遊戲介面標籤 | 對應 JSON 欄位 | 數值範例 | 說明 |
| :--- | :--- | :--- | :--- |
| **主動扔屎** | `combatStats.activeThrows` | `0` | 本輪主動對敵人丟小零食次數 |
| **防衛扔屎** | `combatStats.defenseThrows` | `0` | 本輪防守反擊丟小零食次數 |
| **總主動扔屎** | `totalCombatStats.activeThrows` | `0` | 歷史生涯主動丟小零食累計 |
| **總防衛扔屎** | `totalCombatStats.defenseThrows` | `11` | 歷史生涯防守反擊丟小零食累計 |
| **遭襲沾屎** | `totalCombatStats.assaultSoiled`| `7` | 被敵方主動襲擊丟中沾屎累計 |
| **遭反殺沾屎** | `totalCombatStats.counterSoiled`| `0` | 攻擊敵方時被敵方反殺丟中沾屎累計 |
| **勝場** | `combatStats.wins` | `10` | 當前輪次勝場 |
| **敗場** | `combatStats.losses` | `0` | 當前輪次敗場 |
| **總勝場** | `totalCombatStats.wins` | `417` | 歷史生涯總勝場 |
| **總敗場** | `totalCombatStats.losses` | `130` | 歷史生涯總敗場 |

---

## 4. 全角色清單與轉生配點 (`characters`)

- **端點 URL**：`GET https://mydoujin-backend.onrender.com/api/characters`
- **用途**：取得玩家擁有的全部角色清單、事件偏好名單、最高跳樓層數以及分配轉生點配置。
- **對應 C# Model**：[CharactersResponse.cs](file:///d:/MyDoujinBot/MyDoujinBot/Models/CharactersResponse.cs)

### 核心頂層欄位
| 欄位名稱 | 型別 | 遊戲介面對應名稱 | 說明 |
| :--- | :--- | :--- | :--- |
| `deprioritizedCharacterIds` | `List<string>` | **事件偏好** | 可以減少所選角色的相關事件；共用事件仍可能正常出現（例：`["agil"]`）。 |
| `jumpMaxFloor` | `int` | **跳樓最高層數** | 當前可直接跳躍挑戰之最高層數（例：`41`）。 |
| `characters` | `List<CharacterEntry>` | **角色列表** | 玩家持有的所有角色項目（詳見下方子結構）。 |
| `reincarnation` | `ReincarnationInfo`| **分配轉生點** | 轉生點數儲存、加成配點分配與層數解鎖開關（詳見下方子結構）。 |

### 子結構：角色清單項目 (`characters[] / CharacterEntry`)
| 欄位名稱 | 型別 | 說明 / 範例值 |
| :--- | :--- | :--- |
| `_id` | `string` | 角色實例 ObjectId (例：`"6aace3d256161900a8a0f4c0"`) |
| `userId` | `string` | 擁有者玩家 ObjectId (例：`"6aace3d256161900a8a0f4bf"`) |
| `characterId` | `string` | 角色原型代號 (例：`"kirito"`, `"doujin"`) |
| `formId` | `string?` | 角色型態 ID (未轉變時為 `null`) |
| `level` | `int` | 當前等級 (例：`100`) |
| `exp` | `int` | 當前等級經驗值 (例：`0`) |
| `totalExp` | `int` | 累計總經驗值 (例：`173250`) |
| `combatStats` | `CombatStats` | 本輪戰績統計 (tricks, activeThrows, defenseThrows, assaultSoiled, counterSoiled, wins, losses) |
| `isDead` | `bool` | 是否處於死亡狀態 (例：`true`) |
| `isSoiled` | `bool` | 是否處於沾屎狀態 (例：`true`) |
| `name` | `string` | 角色名稱 (例：`"桐人"`) |
| `title` | `string` | 角色稱號 (例：`"黑色劍士"`) |
| `imagePath` | `string` | 角色立繪圖檔路徑 (例：`"/images/characters/kirito.webp"`) |
| `stats` | `CharacterStats` | 九大戰鬥屬性 (hp, atk, def, sta, agi, spd, tec, int, luk) |
| `reincarnationIdentity` | `ReincarnationIdentity` | 轉生識別外觀資訊 (formId, name, title, imagePath) |
| `legacyLevel` | `int` | 傳承等級 (例：`70`) |
| `reincarnationStats` | `CharacterStats` | 轉生加成數值 (hp, atk, def, sta, agi, spd, tec, int, luk) |
| `normalAttackId` | `string?` | *(自訂同人角色特有)* 普攻技能代號 (例：`"standard_offensive_magic"`) |
| `skillIds` | `List<string>?` | *(自訂同人角色特有)* 配備技能代號清單 |
| `appearance` | `CharacterAppearance?` | *(自訂同人角色特有)* 紙娃娃外觀設定 |

### 子結構：轉生外觀識別 (`reincarnationIdentity`)
| 欄位名稱 | 型別 | 說明 / 範例值 |
| :--- | :--- | :--- |
| `formId` | `string?` | 型態 ID (例：`null`) |
| `name` | `string` | 角色名稱 (例：`"桐人"`) |
| `title` | `string` | 角色稱號 (例：`"黑色劍士"`) |
| `imagePath` | `string` | 角色立繪 (例：`"/images/characters/kirito.webp"`) |

### 子結構：轉生點數與功能解鎖 (`reincarnation`)
| 欄位名稱 | 型別 | 數值範例 | 說明 |
| :--- | :--- | :--- | :--- |
| `total` | `int` | `155` | 已累積總點數 |
| `spent` | `int` | `155` | 本次已分配點數 |
| `available` | `int` | `0` | 剩餘可用點數 |
| `maxPoints` | `int` | `155` | 轉生點數上限 |
| `advancedUnlocked`| `bool` | `true` | 進階配點解鎖標記 |
| `advancedUnlockFloor` | `int` | `31` | 進階配點解鎖樓層 (通關30層，第31層起解鎖) |
| `utilityUnlocked` | `bool` | `true` | 效益配點解鎖標記 |
| `utilityUnlockFloor` | `int` | `21` | 效益配點解鎖樓層 (通關20層，第21層起解鎖) |
| `legacyUnlocked` | `bool` | `false` | 傳承系統解鎖標記 |
| `legacyUnlockFloor` | `int` | `61` | 傳承系統解鎖樓層 (通關60層，第61層起解鎖) |
| `allocated` | `ReincarnationAllocated` | (詳見下方清單) | 具體配點屬性與點數分配物件 |

- **規則記錄**：
  1. 每次轉生都會退回全部舊配點，從零重新分配；未分配的點數會保留。
  2. 角色每升到 16 等以上的每一級可獲得 1 點，上限 `maxPoints` 點。
  3. 歷史最高樓層從 31 層起，每層提高上限 5 點；跳樓後保留，上限提高不直接贈送點數。

### 轉生配點屬性清單 (`reincarnation.allocated`)
| 屬性代號 | 遊戲介面名稱 | 配點範例 | 解鎖條件 | 遊戲內機制說明 |
| :--- | :--- | :--- | :--- | :--- |
| `hp` | **HP** | `30` | 基礎預設 | 生命值，歸零即倒下。 |
| `atk` | **攻擊** | `0` | 基礎預設 | 影響普通攻擊與技能傷害。 |
| `def` | **防禦** | `0` | 基礎預設 | 減少受到的傷害。 |
| `sta` | **體力** | `0` | 基礎預設 | 影響持續作戰能力與部分技能發動。 |
| `agi` | **敏捷** | `0` | 基礎預設 | 影響迴避與攻擊命中。 |
| `spd` | **反應速度** | `0` | 基礎預設 | 決定先攻、連擊次數，以及額外回合。 |
| `tec` | **技巧** | `0` | 基礎預設 | 影響命中、爆擊傷害，以及反擊機率。 |
| `int` | **智力** | `95` | 基礎預設 | 影響法術傷害與治療量。 |
| `luk` | **幸運** | `0` | 基礎預設 | 最神奇的屬性，能在戰鬥中造成意想不到的效果。 |
| `expBonus` | **經驗值加成** | `0` | **20 層通關解鎖** (`utilityUnlocked`) | 所有經驗來源依每點倍率增加。 |
| `eventBonus` | **事件觸發加速** | `30` | **20 層通關解鎖** (`utilityUnlocked`) | 遭遇事件機率依每點倍率增加。 |
| `effortBonus` | **增加行動努力值** | `0` | **30 層通關解鎖** (`advancedUnlocked`) | 提高行動取得的努力值，讓累積的努力值支撐更多次連續升級；不增加每級成長總量。 |
| `aggroIncrease` | **增加仇恨值** | `0` | **30 層通關解鎖** (`advancedUnlocked`) | 提高被依仇恨選敵的攻擊選中的相對權重。 |
| `aggroDecrease` | **減少仇恨值** | `0` | **30 層通關解鎖** (`advancedUnlocked`) | 降低被依仇恨選敵的攻擊選中的相對權重。（增加與減少仇恨同時配點會互相抵銷，仍各自消耗點數） |
| `legacyUnlocked` | **傳承系統** | - | **60 層通關解鎖** (`legacyUnlockFloor: 61`) | 通關第 60 層後解鎖傳承：轉生時保留一半的總經驗值與對應的養成。 |

---

## 5. 訓練與探險行動 (`action/explore`)

- **端點 URL**：`POST https://mydoujin-backend.onrender.com/api/action/explore`
- **用途**：執行指定的訓練/探險行動，回傳本次行動冷卻、是否滿等、出戰角色即時資料、行動結算結果，以及是否觸發突發事件。
- **Request Payload**：`{ "actionId": "charity" }`（例：`hunt`, `training`, `picnic`, `flirt`, `charity`, `rest`, `fishing`）
- **對應 C# Model**：[TrainingResponse.cs](file:///d:/MyDoujinBot/MyDoujinBot/Models/TrainingResponse.cs)

### 頂層欄位結構 (Root)
| 欄位名稱 | 型別 | 說明 / 範例值 |
| :--- | :--- | :--- |
| `lastActionAt` | `DateTimeOffset` (string) | 上次執行行動之 UTC 時間戳記 (例：`"2026-10-16T14:39:14.996Z"`) |
| `serverTime` | `DateTimeOffset` (string) | 伺服器當前 UTC 時間戳記 (例：`"2026-10-16T14:39:15.012Z"`) |
| `cooldownRemainingMs` | `int` | 當前剩餘冷卻毫秒數 (例：`9984`) |
| `cooldownMs` | `int` | 該行動之基礎冷卻毫秒數 (例：`10000` = 10 秒) |
| `pendingEvent` | `PendingEvent?` (object / null) | **突發事件**：若觸發事件則為事件詳情與選項清單，無事件時為 `null` |
| `isMaxLevel` | `bool` | 當前出戰角色是否已達等級上限 (例：`false`) |
| `activeCharacter` | `ActiveCharacter` (object) | **當前出戰角色詳情物件** (詳見下方子結構) |
| `actionResult` | `ActionResult` (object) | **本次行動結算結果物件** (詳見下方子結構) |

### 子結構：出戰角色詳情 (`activeCharacter`)
| 欄位名稱 | 型別 | 說明 / 數值範例 |
| :--- | :--- | :--- |
| `_id` | `string` | 角色實例 ObjectId (例：`"6acf0df8734f55ff241d9943"`) |
| `userId` | `string` | 玩家帳號 ObjectId (例：`"6aace3d256161900a8a0f4bf"`) |
| `characterId` | `string` | 角色原型代號 (例：`"doujin"`) |
| `formId` | `string?` | 角色型態 ID (未轉變或無時為 `null`) |
| `level` | `int` | 當前等級 (例：`74`) |
| `exp` | `int` | 當前等級累積經驗值 (例：`1540`) |
| `totalExp` | `int` | 累計總經驗值 (例：`96075`) |
| `combatStats` | `CombatStats` | **本輪戰績統計** (含 throws、wins、losses 等，詳見下方戰績表) |
| `isDead` | `bool` | 是否處於死亡狀態 (例：`false`) |
| `isSoiled` | `bool` | 是否處於沾屎狀態 (例：`false`) |
| `name` | `string` | 角色名稱 (例：`"同人"`) |
| `title` | `string` | 角色稱號 (例：`"我的同人"`) |
| `imagePath` | `string` | 角色立繪圖檔路徑 (例：`"/images/characters/kirito.webp"`) |
| `stats` | `CharacterStats` | **九大戰鬥屬性** (`hp: 9580`, `atk: 183`, `def: 373`, `sta: 199`, `agi: 192`, `spd: 172`, `tec: 130`, `int: 618`, `luk: 115`) |
| `normalAttackId` | `string` | 普攻技能代號 (例：`"standard_offensive_magic"`) |
| `skillIds` | `List<string>` | 配備技能代號清單 (例：`["minor_erdtree", "first_aid", "simmer_staff_meal"]`) |
| `appearance` | `CharacterAppearance` | **外觀紙娃娃設定** (含 frontHair, backHair, brows, eyes, skin, outfit 及對應 Style 字串) |
| `totalCombatStats` | `CombatStats` | **生涯累計戰績統計** (含 throws、wins、losses 等，詳見下方戰績表) |
| `reincarnationCount` | `int` | 累計轉生次數 (例：`22`) |

### 子結構：外觀樣式 (`activeCharacter.appearance`)
| 欄位名稱 | 型別 | 範例值 | 說明 |
| :--- | :--- | :--- | :--- |
| `frontHair` | `string` | `"#f7f5ad"` | 前髮顏色 HEX |
| `backHair` | `string` | `"#000091"` | 後髮顏色 HEX |
| `brows` | `string` | `"#5c3900"` | 眉毛顏色 HEX |
| `eyes` | `string` | `"#05ab3a"` | 眼睛顏色 HEX |
| `skin` | `string` | `"#edbb82"` | 膚色 HEX |
| `outfit` | `string` | `"#450000"` | 服裝顏色 HEX |
| `frontHairStyle` | `string` | `"crop"` | 前髮造型代號 |
| `backHairStyle` | `string` | `"short"` | 後髮造型代號 |
| `browStyle` | `string` | `"bold"` | 眉毛造型代號 |
| `eyeStyle` | `string` | `"almond"` | 眼型代號 |
| `outfitStyle` | `string` | `"hoodie"` | 服裝造型代號 |

### 子結構：戰績統計 (`combatStats` 與 `totalCombatStats`)
| 欄位名稱 | 型別 | 本輪數值 (`combatStats`) | 生涯數值 (`totalCombatStats`) | 說明 |
| :--- | :--- | :--- | :--- | :--- |
| `activeThrows` | `int` | `0` | `0` | 主動扔屎次數 |
| `defenseThrows` | `int` | `0` | `11` | 防衛扔屎次數 |
| `assaultSoiled` | `int` | `0` | `7` | 遭襲沾屎次數 |
| `counterSoiled` | `int` | `0` | `0` | 遭反殺沾屎次數 |
| `wins` | `int` | `10` | `417` | 勝場次數 |
| `losses` | `int` | `0` | `130` | 敗場次數 |
| `tricks` | `int` | `0` | `0` | 特殊伎倆次數 |

### 子結構：行動結果 (`actionResult`)
| 欄位名稱 | 型別 | 說明 / 數值範例 |
| :--- | :--- | :--- |
| `actionId` | `string` | 執行的行動代號 (例：`"charity"`) |
| `actionName` | `string` | 執行的行動中文名稱 (例：`"做善事"`) |
| `exp` | `int` | 本次行動獲得之經驗值 (例：`20`) |
| `level` | `int` | 行動結算後角色等級 (例：`74`) |
| `levelUps` | `int` | 本次行動觸發之升級次數 (例：`0`) |
| `gains` | `Dictionary<string, int>` | 各能力屬性成長值 (`hp`, `atk`, `def`, `sta`, `agi`, `spd`, `tec`, `int`, `luk`，範例值皆為 `0`) |
