using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using MyDoujinBot.Models;

namespace MyDoujinBot.Services
{
    /// <summary>
    /// 通用方案管理服務。
    /// 提供獨立資料夾儲存、列舉、讀寫、刪除以及跨機器匯入/匯出功能。
    /// 適用於階梯排程 (Pipeline) 及未來之自訂事件規則 (Event Rules)。
    /// </summary>
    /// <typeparam name="T">實作 IPresetProfile 之方案類別</typeparam>
    public class PresetManager<T> where T : class, IPresetProfile, new()
    {
        private readonly string _category;
        private readonly string _storageDir;
        private static readonly JsonSerializerOptions _jsonOptions = new()
        {
            WriteIndented = true,
            PropertyNameCaseInsensitive = true
        };

        public string Category => _category;
        public string StorageDirectory => _storageDir;

        public PresetManager(string category)
        {
            if (string.IsNullOrWhiteSpace(category))
                throw new ArgumentException("方案分類名稱不得為空", nameof(category));

            _category = category.Trim().ToLowerInvariant();
            _storageDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "data", "presets", _category);

            try
            {
                Directory.CreateDirectory(_storageDir);
            }
            catch
            {
                // 忽略建立目錄失敗
            }
        }

        /// <summary>
        /// 取得儲存目錄下所有方案。
        /// </summary>
        public List<T> LoadAll()
        {
            var list = new List<T>();
            if (!Directory.Exists(_storageDir)) return list;

            var files = Directory.GetFiles(_storageDir, "*.json");
            foreach (var file in files)
            {
                try
                {
                    var json = File.ReadAllText(file);
                    var item = JsonSerializer.Deserialize<T>(json, _jsonOptions);
                    if (item != null && !string.IsNullOrWhiteSpace(item.Id))
                    {
                        list.Add(item);
                    }
                }
                catch
                {
                    // 略過損壞檔案
                }
            }

            return list.OrderBy(p => p.CreatedAt).ToList();
        }

        /// <summary>
        /// 依 ID 取得單一方案。
        /// </summary>
        public T? GetById(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return null;
            string filePath = GetFilePath(id);
            if (!File.Exists(filePath)) return null;

            try
            {
                var json = File.ReadAllText(filePath);
                return JsonSerializer.Deserialize<T>(json, _jsonOptions);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// 儲存方案（新增或覆寫）。
        /// </summary>
        public void Save(T preset)
        {
            if (preset == null) throw new ArgumentNullException(nameof(preset));
            if (string.IsNullOrWhiteSpace(preset.Id))
                preset.Id = Guid.NewGuid().ToString("N");

            preset.UpdatedAt = DateTime.Now;
            Directory.CreateDirectory(_storageDir);

            string filePath = GetFilePath(preset.Id);
            var json = JsonSerializer.Serialize(preset, _jsonOptions);
            File.WriteAllText(filePath, json);
        }

        /// <summary>
        /// 刪除指定方案檔案。
        /// </summary>
        public bool Delete(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return false;
            string filePath = GetFilePath(id);
            if (File.Exists(filePath))
            {
                try
                {
                    File.Delete(filePath);
                    return true;
                }
                catch
                {
                    return false;
                }
            }
            return false;
        }

        /// <summary>
        /// 將方案以統一封裝格式匯出至外部檔案。
        /// </summary>
        public void ExportToFile(T preset, string targetFilePath)
        {
            if (preset == null) throw new ArgumentNullException(nameof(preset));
            if (string.IsNullOrWhiteSpace(targetFilePath)) throw new ArgumentException("目標檔案路徑不得為空", nameof(targetFilePath));

            var package = new PresetPackage<T>
            {
                SchemaVersion = 1,
                Category = _category,
                ExportedAt = DateTime.Now,
                Preset = preset
            };

            var dir = Path.GetDirectoryName(targetFilePath);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

            var json = JsonSerializer.Serialize(package, _jsonOptions);
            File.WriteAllText(targetFilePath, json);
        }

        /// <summary>
        /// 從外部檔案匯入方案。
        /// 支援標準封裝格式與原始物件格式。
        /// 匯入成功後會自動給予新 ID 儲存至本地，並回傳實體。
        /// </summary>
        public T ImportFromFile(string sourceFilePath)
        {
            if (!File.Exists(sourceFilePath))
                throw new FileNotFoundException("找不到指定的方案檔案", sourceFilePath);

            var json = File.ReadAllText(sourceFilePath);
            T? imported = null;

            // 1. 嘗試解析為標準封裝包 (PresetPackage<T>)
            try
            {
                var package = JsonSerializer.Deserialize<PresetPackage<T>>(json, _jsonOptions);
                if (package != null && package.Preset != null)
                {
                    // 驗證分類一致性（若有標記）
                    if (!string.IsNullOrEmpty(package.Category) &&
                        !string.Equals(package.Category, _category, StringComparison.OrdinalIgnoreCase))
                    {
                        throw new InvalidOperationException($"檔案分類不吻合：此檔案為「{package.Category}」方案，無法匯入至「{_category}」！");
                    }
                    imported = package.Preset;
                }
            }
            catch (InvalidOperationException)
            {
                throw;
            }
            catch
            {
                // 忽略封裝包解析錯誤，嘗試回退為裸物件
            }

            // 2. 若非封裝包，嘗試直接解析為裸物件 T
            if (imported == null)
            {
                try
                {
                    imported = JsonSerializer.Deserialize<T>(json, _jsonOptions);
                }
                catch (Exception ex)
                {
                    throw new InvalidOperationException($"檔案格式無法識別為有效的方案資料：{ex.Message}", ex);
                }
            }

            if (imported == null)
            {
                throw new InvalidOperationException("檔案內容解析為空，無法匯入。");
            }

            // 匯入時產生新識別碼，避免與現有方案 ID 重複衝突
            imported.Id = Guid.NewGuid().ToString("N");
            imported.UpdatedAt = DateTime.Now;

            // 儲存至本地資料夾
            Save(imported);
            return imported;
        }

        private string GetFilePath(string id)
        {
            return Path.Combine(_storageDir, $"{id}.json");
        }
    }
}
