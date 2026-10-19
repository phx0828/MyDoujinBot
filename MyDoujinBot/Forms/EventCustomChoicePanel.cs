using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using FontAwesome.Sharp;
using MyDoujinBot.Models;
using MyDoujinBot.Services;
using MyDoujinBot.Utilities;

namespace MyDoujinBot.Forms
{
    /// <summary>
    /// 事件自選設定面板。
    /// 包含啟用開關、方案切換/管理工具列，以及開啟大視窗編輯之按鈕。
    /// </summary>
    public sealed class EventCustomChoicePanel : Panel
    {
        private readonly EventArchiveService _archiveService;
        private readonly PresetManager<EventChoicePreset> _presetManager = new("event");

        private EventChoicePreset _activePreset = null!;

        // 介面控制項
        private CheckBox _chkEnable = null!;
        private FlowLayoutPanel _pnlPresetContainer = null!;
        private FlowLayoutPanel _pnlPresetActions = null!;
        private ComboBox _cmbPresets = null!;
        private IconButton _btnEdit = null!;
        private IconButton _btnNewPreset = null!;
        private IconButton _btnCopyPreset = null!;
        private IconButton _btnDeletePreset = null!;
        private IconButton _btnExportPreset = null!;
        private IconButton _btnImportPreset = null!;
        private Label _lblSummary = null!;

        private float DpiScale => (float)this.DeviceDpi / 96f;
        private int ScaleDpi(int value) => (int)Math.Round(value * DpiScale);

        public EventCustomChoicePanel(EventArchiveService archiveService)
        {
            _archiveService = archiveService;

            this.Dock = DockStyle.Top;
            this.AutoSize = true;
            this.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            this.BackColor = Color.Transparent;
            this.Padding = new Padding(ScaleDpi(16), 0, 0, 0);
            this.Margin = new Padding(0, 0, 0, ScaleDpi(12));

            InitializePresets();
            BuildUI();
            UpdateSummaryLabel();
        }

        /// <summary>
        /// 查詢指定 eventId 之目標 optionId。時間複雜度 O(1)。
        /// 若使用者未勾選啟用事件指定選項，則回傳 null。
        /// </summary>
        public string? ResolveChoice(string eventId)
        {
            if (!AppSettingsManager.Current.EnableEventCustomChoice)
            {
                return null;
            }

            if (_activePreset?.Choices != null && _activePreset.Choices.TryGetValue(eventId, out var optionId))
            {
                return optionId;
            }
            return null;
        }

        /// <summary>
        /// 當本機事件檔案庫更新後呼叫，重新載入記憶體索引並刷新摘要。
        /// </summary>
        public void ReloadRegistry()
        {
            UpdateSummaryLabel();
        }

        private void InitializePresets()
        {
            var presets = _presetManager.LoadAll();
            if (presets.Count == 0)
            {
                var initial = new EventChoicePreset
                {
                    Name = "預設方案",
                    Choices = new Dictionary<string, string>()
                };
                _presetManager.Save(initial);
                presets.Add(initial);
            }

            string activeId = AppSettingsManager.Current.ActiveEventPresetId;
            _activePreset = presets.FirstOrDefault(p => p.Id == activeId) ?? presets[0];
            AppSettingsManager.Current.ActiveEventPresetId = _activePreset.Id;
        }

        private void BuildUI()
        {
            // ── Row 0: 啟用事件指定選項核取方塊 ──
            _chkEnable = new CheckBox
            {
                Text = "啟用事件指定選項（未啟用或未指定之事件將依上方自動/手動設定處理）",
                Checked = AppSettingsManager.Current.EnableEventCustomChoice,
                ForeColor = Color.FromArgb(215, 215, 230),
                Font = new Font("Microsoft JhengHei UI", 10f),
                Dock = DockStyle.Top,
                AutoSize = true,
                Margin = new Padding(0, 0, 0, ScaleDpi(4))
            };
            _chkEnable.CheckedChanged += (s, e) =>
            {
                AppSettingsManager.Current.EnableEventCustomChoice = _chkEnable.Checked;
                AppSettingsManager.Save();
                UpdateEnabledState();
            };

            // ── Row 1: 方案選擇與核心操作列 (自選方案、下拉選單、編輯方案、刪除) ──
            _pnlPresetContainer = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = true,
                BackColor = Color.Transparent,
                Padding = Padding.Empty,
                Margin = new Padding(0, 0, 0, ScaleDpi(4))
            };

            var lblPresetTitle = new Label
            {
                Text = "自選方案：",
                ForeColor = Color.FromArgb(210, 210, 235),
                Font = new Font("Microsoft JhengHei UI", 9.8f, FontStyle.Bold),
                AutoSize = true,
                Margin = new Padding(0, ScaleDpi(5), ScaleDpi(4), 0)
            };

            _cmbPresets = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Width = ScaleDpi(165),
                BackColor = Color.FromArgb(40, 42, 54),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Microsoft JhengHei UI", 9.5f),
                DisplayMember = nameof(EventChoicePreset.Name),
                Margin = new Padding(0, ScaleDpi(2), ScaleDpi(8), ScaleDpi(4))
            };
            _cmbPresets.SelectedIndexChanged += OnPresetSelectionChanged;

            _btnEdit = CreateCompactButton("編輯方案", Color.FromArgb(45, 95, 150), IconChar.PenToSquare);
            _btnEdit.Font = new Font("Microsoft JhengHei UI", 9.5f, FontStyle.Bold);
            _btnEdit.Click += (s, e) => OpenEditorDialog();

            _btnDeletePreset = CreateCompactButton("刪除", Color.FromArgb(85, 45, 45), IconChar.TrashCan);
            _btnDeletePreset.Click += (s, e) => DeleteActivePreset();

            _pnlPresetContainer.Controls.Add(lblPresetTitle);
            _pnlPresetContainer.Controls.Add(_cmbPresets);
            _pnlPresetContainer.Controls.Add(_btnEdit);
            _pnlPresetContainer.Controls.Add(_btnDeletePreset);

            // ── Row 2: 方案操作工具列 (開新方案、複製方案、匯出、匯入) ──
            _pnlPresetActions = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = true,
                BackColor = Color.Transparent,
                Padding = Padding.Empty,
                Margin = new Padding(0, 0, 0, ScaleDpi(6))
            };

            _btnNewPreset = CreateCompactButton("開新方案", Color.FromArgb(42, 110, 75), IconChar.Plus);
            _btnNewPreset.Click += (s, e) => CreateNewPreset();

            _btnCopyPreset = CreateCompactButton("複製方案", Color.FromArgb(48, 95, 115), IconChar.Clone);
            _btnCopyPreset.Click += (s, e) => CopyActivePreset();

            _btnExportPreset = CreateCompactButton("匯出", Color.FromArgb(48, 65, 90), IconChar.FileExport);
            _btnExportPreset.Click += (s, e) => ExportActivePreset();

            _btnImportPreset = CreateCompactButton("匯入", Color.FromArgb(48, 65, 90), IconChar.FileImport);
            _btnImportPreset.Click += (s, e) => ImportPresetFromFile();

            _pnlPresetActions.Controls.Add(_btnNewPreset);
            _pnlPresetActions.Controls.Add(_btnCopyPreset);
            _pnlPresetActions.Controls.Add(_btnExportPreset);
            _pnlPresetActions.Controls.Add(_btnImportPreset);

            // ── Row 3: 方案摘要說明標籤 ──
            _lblSummary = new Label
            {
                Dock = DockStyle.Top,
                ForeColor = Color.FromArgb(145, 155, 175),
                Font = new Font("Microsoft JhengHei UI", 9.5f),
                AutoSize = true,
                Margin = new Padding(0, 0, 0, ScaleDpi(8))
            };

            // WinForms 的 DockStyle.Top 排版依控制項 Z-Order 堆疊，後加入者置頂，因此倒序加入確保視覺順序由上至下 (Row 0 -> Row 1 -> Row 2 -> Row 3)
            this.Controls.Add(_lblSummary);
            this.Controls.Add(_pnlPresetActions);
            this.Controls.Add(_pnlPresetContainer);
            this.Controls.Add(_chkEnable);

            RefreshPresetComboBox();
            UpdateEnabledState();
        }

        private void UpdateEnabledState()
        {
            // 啟用與否不鎖定方案選擇與編輯等功能
            _pnlPresetContainer.Enabled = true;
            _pnlPresetActions.Enabled = true;
            _lblSummary.ForeColor = Color.FromArgb(145, 155, 175);
        }

        private void UpdateSummaryLabel()
        {
            if (_lblSummary == null || _activePreset == null) return;
            int count = _activePreset.Choices?.Count ?? 0;
            _lblSummary.Text = $"當前方案已指定 {count} 個事件選項。";
        }

        private void RefreshPresetComboBox(string? selectId = null)
        {
            if (_cmbPresets == null) return;

            _cmbPresets.SelectedIndexChanged -= OnPresetSelectionChanged;
            _cmbPresets.Items.Clear();

            var presets = _presetManager.LoadAll();
            int selectedIndex = 0;
            string targetId = selectId ?? _activePreset?.Id ?? string.Empty;

            for (int i = 0; i < presets.Count; i++)
            {
                _cmbPresets.Items.Add(presets[i]);
                if (presets[i].Id == targetId)
                {
                    selectedIndex = i;
                }
            }

            if (_cmbPresets.Items.Count > 0)
            {
                _cmbPresets.SelectedIndex = selectedIndex;
                _activePreset = (EventChoicePreset)_cmbPresets.Items[selectedIndex]!;
            }

            _cmbPresets.SelectedIndexChanged += OnPresetSelectionChanged;
        }

        private void OnPresetSelectionChanged(object? sender, EventArgs e)
        {
            if (_cmbPresets.SelectedItem is EventChoicePreset selected)
            {
                if (_activePreset == null || _activePreset.Id != selected.Id)
                {
                    _activePreset = _presetManager.GetById(selected.Id) ?? selected;
                    AppSettingsManager.Current.ActiveEventPresetId = _activePreset.Id;
                    AppSettingsManager.Save();
                    UpdateSummaryLabel();
                }
            }
        }

        private void OpenEditorDialog()
        {
            if (_activePreset == null) return;

            var otherNames = _presetManager.LoadAll()
                .Where(p => p.Id != _activePreset.Id)
                .Select(p => p.Name);

            using var editor = new EventChoiceEditorForm(_archiveService, _activePreset, otherNames);
            if (editor.ShowDialog(this.FindForm()) == DialogResult.OK)
            {
                _presetManager.Save(_activePreset);
                RefreshPresetComboBox(_activePreset.Id);
                UpdateSummaryLabel();
            }
        }

        private void CreateNewPreset()
        {
            var existingNames = _presetManager.LoadAll().Select(p => p.Name).ToHashSet();
            string baseName = "新方案";
            string newName = baseName;
            int counter = 2;
            while (existingNames.Contains(newName))
            {
                newName = $"{baseName} {counter++}";
            }

            var newPreset = new EventChoicePreset
            {
                Name = newName,
                Choices = new Dictionary<string, string>()
            };

            using var editor = new EventChoiceEditorForm(_archiveService, newPreset, existingNames);
            if (editor.ShowDialog(this.FindForm()) == DialogResult.OK)
            {
                _presetManager.Save(newPreset);
                _activePreset = newPreset;
                AppSettingsManager.Current.ActiveEventPresetId = newPreset.Id;
                AppSettingsManager.Save();

                RefreshPresetComboBox(newPreset.Id);
                UpdateSummaryLabel();
            }
        }

        private void CopyActivePreset()
        {
            if (_activePreset == null) return;

            var existingNames = _presetManager.LoadAll().Select(p => p.Name).ToHashSet();
            string baseName = $"{_activePreset.Name} (複製)";
            string copyName = baseName;
            int counter = 2;
            while (existingNames.Contains(copyName))
            {
                copyName = $"{_activePreset.Name} (複製 {counter++})";
            }

            var copiedPreset = _activePreset.Clone(copyName);
            _presetManager.Save(copiedPreset);
            _activePreset = copiedPreset;
            AppSettingsManager.Current.ActiveEventPresetId = copiedPreset.Id;
            AppSettingsManager.Save();

            RefreshPresetComboBox(copiedPreset.Id);
            UpdateSummaryLabel();
        }

        private void DeleteActivePreset()
        {
            var presets = _presetManager.LoadAll();
            if (presets.Count <= 1)
            {
                MessageBox.Show(this.FindForm(), "必須保留至少一個事件自選方案，無法刪除。", "無法刪除", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var confirm = MessageBox.Show(this.FindForm(), $"確定要刪除自選方案【{_activePreset.Name}】嗎？此動作無法復原。", "刪除確認", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (confirm != DialogResult.Yes) return;

            _presetManager.Delete(_activePreset.Id);

            presets = _presetManager.LoadAll();
            _activePreset = presets[0];
            AppSettingsManager.Current.ActiveEventPresetId = _activePreset.Id;
            AppSettingsManager.Save();

            RefreshPresetComboBox(_activePreset.Id);
            UpdateSummaryLabel();
        }

        private void ExportActivePreset()
        {
            using var sfd = new SaveFileDialog
            {
                Title = "匯出事件自選方案",
                Filter = "JSON 方案檔案 (*.json)|*.json",
                FileName = $"{_activePreset.Name}.json"
            };

            if (sfd.ShowDialog(this.FindForm()) != DialogResult.OK) return;

            try
            {
                _presetManager.ExportToFile(_activePreset, sfd.FileName);
                MessageBox.Show(this.FindForm(), $"方案已成功匯出至：\n{sfd.FileName}", "匯出成功", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this.FindForm(), $"匯出方案失敗：\n{ex.Message}", "匯出失敗", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void ImportPresetFromFile()
        {
            using var ofd = new OpenFileDialog
            {
                Title = "匯入事件自選方案",
                Filter = "JSON 方案檔案 (*.json)|*.json"
            };

            if (ofd.ShowDialog(this.FindForm()) != DialogResult.OK) return;

            try
            {
                var imported = _presetManager.ImportFromFile(ofd.FileName);
                if (imported != null)
                {
                    _activePreset = imported;
                    AppSettingsManager.Current.ActiveEventPresetId = imported.Id;
                    AppSettingsManager.Save();

                    RefreshPresetComboBox(imported.Id);
                    UpdateSummaryLabel();
                    MessageBox.Show(this.FindForm(), $"方案【{imported.Name}】已成功匯入！", "匯入成功", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(this.FindForm(), $"匯入方案失敗：\n{ex.Message}", "匯入失敗", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private IconButton CreateCompactButton(string text, Color backColor, IconChar icon)
        {
            var btn = new IconButton
            {
                Text = $" {text}",
                AutoSize = true,
                BackColor = backColor,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Microsoft JhengHei UI", 9.5f),
                Cursor = Cursors.Hand,
                IconChar = icon,
                IconColor = Color.White,
                IconSize = ScaleDpi(16),
                TextImageRelation = TextImageRelation.ImageBeforeText,
                ImageAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(ScaleDpi(8), ScaleDpi(4), ScaleDpi(8), ScaleDpi(4)),
                Margin = new Padding(0, ScaleDpi(1), ScaleDpi(6), ScaleDpi(4))
            };
            btn.FlatAppearance.BorderSize = 0;
            return btn;
        }
    }
}
