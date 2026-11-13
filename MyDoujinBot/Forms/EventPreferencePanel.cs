using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using FontAwesome.Sharp;
using MyDoujinBot.Models;
using MyDoujinBot.Services;

namespace MyDoujinBot.Forms
{
    /// <summary>
    /// 事件偏好面板（事件設定頁）：方案選擇與管理、開啟編輯視窗、將待更新清單送出伺服器。
    /// 風格與操作邏輯比照 EventCustomChoicePanel；切換或編輯方案只會套用到「待更新清單」，按「更新」後才 PUT 至伺服器。
    /// </summary>
    public sealed class EventPreferencePanel : Panel
    {
        private readonly EventArchiveService _archiveService;
        private readonly Func<string> _tokenProvider;
        private readonly EventPreferenceService _service = new();
        private readonly PresetManager<EventPreferencePreset> _presetManager = new("eventpref");

        private EventPreferencePreset _activePreset = null!;
        private bool _syncInitialPresetOnFirstProfileLoad;

        private FlowLayoutPanel _pnlPresetContainer = null!;
        private FlowLayoutPanel _pnlPresetActions = null!;
        private ComboBox _cmbPresets = null!;
        private IconButton _btnEdit = null!;
        private IconButton _btnUpdate = null!;
        private IconButton _btnDeletePreset = null!;
        private IconButton _btnNewPreset = null!;
        private IconButton _btnCopyPreset = null!;
        private IconButton _btnExportPreset = null!;
        private IconButton _btnImportPreset = null!;
        private Label _lblNotice = null!;
        private Label _lblSelectedNames = null!;
        private Label _lblSummary = null!;

        private float DpiScale => (float)this.DeviceDpi / 96f;
        private int ScaleDpi(int value) => (int)Math.Round(value * DpiScale);

        public EventPreferencePanel(EventArchiveService archiveService, Func<string> tokenProvider)
        {
            _archiveService = archiveService;
            _tokenProvider = tokenProvider;

            this.Dock = DockStyle.Top;
            this.AutoSize = true;
            this.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            this.BackColor = Color.Transparent;
            this.Padding = new Padding(ScaleDpi(16), 0, 0, 0);
            this.Margin = new Padding(0, 0, 0, ScaleDpi(12));

            InitializePresets();
            BuildUI();
            UpdateSummaryLabel();

            UserContext.OnProfileUpdated += OnProfileUpdated;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                UserContext.OnProfileUpdated -= OnProfileUpdated;
                _service.Dispose();
            }
            base.Dispose(disposing);
        }

        private void OnProfileUpdated()
        {
            if (this.IsDisposed || !this.IsHandleCreated) return;
            try
            {
                this.BeginInvoke(() =>
                {
                    if (_syncInitialPresetOnFirstProfileLoad &&
                        UserContext.Current?.CharactersData != null)
                    {
                        _syncInitialPresetOnFirstProfileLoad = false;
                        if (_activePreset != null && _activePreset.DeprioritizedCharacterIds.Count == 0)
                        {
                            var serverIds = ServerIds();
                            if (serverIds.Count > 0)
                            {
                                _activePreset.DeprioritizedCharacterIds = serverIds.ToList();
                                _presetManager.Save(_activePreset);
                            }
                        }
                    }
                    UpdateSummaryLabel();
                });
            }
            catch { }
        }

        private static List<string> ServerIds() =>
            UserContext.Current?.CharactersData?.DeprioritizedCharacterIds ?? new List<string>();

        private void InitializePresets()
        {
            var presets = _presetManager.LoadAll();
            if (presets.Count == 0)
            {
                var serverIds = ServerIds();
                _syncInitialPresetOnFirstProfileLoad = UserContext.Current?.CharactersData == null;
                var initial = new EventPreferencePreset
                {
                    Name = "預設方案",
                    DeprioritizedCharacterIds = serverIds.ToList()
                };
                _presetManager.Save(initial);
                presets.Add(initial);
            }

            string activeId = AppSettingsManager.Current.ActiveEventPreferencePresetId;
            _activePreset = presets.FirstOrDefault(p => p.Id == activeId) ?? presets[0];
            AppSettingsManager.Current.ActiveEventPreferencePresetId = _activePreset.Id;
        }

        private void BuildUI()
        {
            // ── Row 1: 方案選擇與核心操作列 (偏好方案、下拉選單、編輯方案、更新、刪除) ──
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
                Text = "偏好方案：",
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
                DisplayMember = nameof(EventPreferencePreset.Name),
                Margin = new Padding(0, ScaleDpi(2), ScaleDpi(8), ScaleDpi(4))
            };
            _cmbPresets.SelectedIndexChanged += OnPresetSelectionChanged;

            _btnUpdate = CreateCompactButton("更新", Color.FromArgb(42, 110, 75), IconChar.CloudArrowUp);
            _btnUpdate.Font = new Font("Microsoft JhengHei UI", 9.5f, FontStyle.Bold);
            _btnUpdate.Click += async (_, _) => await SubmitAsync();

            _btnEdit = CreateCompactButton("編輯方案", Color.FromArgb(45, 95, 150), IconChar.PenToSquare);
            _btnEdit.Font = new Font("Microsoft JhengHei UI", 9.5f, FontStyle.Bold);
            _btnEdit.Click += (_, _) => OpenEditorDialog();

            _btnDeletePreset = CreateCompactButton("刪除", Color.FromArgb(85, 45, 45), IconChar.TrashCan);
            _btnDeletePreset.Click += (_, _) => DeleteActivePreset();

            _pnlPresetContainer.Controls.Add(lblPresetTitle);
            _pnlPresetContainer.Controls.Add(_cmbPresets);
            _pnlPresetContainer.Controls.Add(_btnUpdate);
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
                Padding = new Padding(0, 0, 0, ScaleDpi(4)),
                Margin = Padding.Empty
            };

            _btnNewPreset = CreateCompactButton("開新方案", Color.FromArgb(42, 110, 75), IconChar.Plus);
            _btnNewPreset.Click += (_, _) => CreateNewPreset();

            _btnCopyPreset = CreateCompactButton("複製方案", Color.FromArgb(48, 95, 115), IconChar.Clone);
            _btnCopyPreset.Click += (_, _) => CopyActivePreset();

            _btnExportPreset = CreateCompactButton("匯出", Color.FromArgb(48, 65, 90), IconChar.FileExport);
            _btnExportPreset.Click += (_, _) => ExportActivePreset();

            _btnImportPreset = CreateCompactButton("匯入", Color.FromArgb(48, 65, 90), IconChar.FileImport);
            _btnImportPreset.Click += (_, _) => ImportPresetFromFile();

            _pnlPresetActions.Controls.Add(_btnNewPreset);
            _pnlPresetActions.Controls.Add(_btnCopyPreset);
            _pnlPresetActions.Controls.Add(_btnExportPreset);
            _pnlPresetActions.Controls.Add(_btnImportPreset);

            // ── Row 3: 橘色提示字（使用 Padding 以在 Dock=Top 下產生有效行距） ──
            _lblNotice = new Label
            {
                Dock = DockStyle.Top,
                Text = "💡 需按下更新後才會同步儲存事件偏好！",
                ForeColor = Color.FromArgb(200, 160, 90),
                Font = new Font("Microsoft JhengHei UI", 9.0f),
                AutoSize = true,
                Padding = new Padding(0, ScaleDpi(2), 0, ScaleDpi(4))
            };

            // ── Row 4: 所選減少事件角色名單 ──
            _lblSelectedNames = new Label
            {
                Dock = DockStyle.Top,
                ForeColor = Color.FromArgb(145, 155, 175),
                Font = new Font("Microsoft JhengHei UI", 9.5f),
                AutoSize = true,
                MaximumSize = new Size(ScaleDpi(680), 0),
                Padding = new Padding(0, ScaleDpi(2), 0, ScaleDpi(4))
            };

            // ── Row 5: 方案與伺服器同步狀態摘要標籤 ──
            _lblSummary = new Label
            {
                Dock = DockStyle.Top,
                ForeColor = Color.FromArgb(145, 155, 175),
                Font = new Font("Microsoft JhengHei UI", 9.5f),
                AutoSize = true,
                Padding = new Padding(0, ScaleDpi(2), 0, ScaleDpi(8))
            };

            // DockStyle.Top 後加入者置頂，故倒序加入
            this.Controls.Add(_lblSummary);
            this.Controls.Add(_lblSelectedNames);
            this.Controls.Add(_lblNotice);
            this.Controls.Add(_pnlPresetActions);
            this.Controls.Add(_pnlPresetContainer);

            RefreshPresetComboBox();
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
                _activePreset = (EventPreferencePreset)_cmbPresets.Items[selectedIndex]!;
            }

            _cmbPresets.SelectedIndexChanged += OnPresetSelectionChanged;
        }

        private void OnPresetSelectionChanged(object? sender, EventArgs e)
        {
            if (_cmbPresets.SelectedItem is EventPreferencePreset selected)
            {
                if (_activePreset == null || _activePreset.Id != selected.Id)
                {
                    _syncInitialPresetOnFirstProfileLoad = false;
                    _activePreset = _presetManager.GetById(selected.Id) ?? selected;
                    AppSettingsManager.Current.ActiveEventPreferencePresetId = _activePreset.Id;
                    AppSettingsManager.Save();
                    UpdateSummaryLabel();
                }
            }
        }

        private void OpenEditorDialog()
        {
            if (_activePreset == null) return;

            var data = UserContext.Current?.CharactersData;
            var route = _archiveService.LoadRouteFile();
            if (data == null || data.Characters.Count == 0)
            {
                MessageBox.Show(this.FindForm(), "尚未取得角色清單，請先於系統設定驗證 Token。", "無法編輯", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            if (route == null)
            {
                MessageBox.Show(this.FindForm(), "本機事件庫讀取失敗。", "無法編輯", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var otherNames = _presetManager.LoadAll()
                .Where(p => p.Id != _activePreset.Id)
                .Select(p => p.Name);

            using var editor = new EventPreferenceEditorForm(data.Characters, route, _activePreset, otherNames);
            if (editor.ShowDialog(this.FindForm()) == DialogResult.OK)
            {
                _syncInitialPresetOnFirstProfileLoad = false;
                _presetManager.Save(_activePreset);
                RefreshPresetComboBox(_activePreset.Id);
                UpdateSummaryLabel();
            }
        }

        private void CreateNewPreset()
        {
            var data = UserContext.Current?.CharactersData;
            var route = _archiveService.LoadRouteFile();
            if (data == null || data.Characters.Count == 0)
            {
                MessageBox.Show(this.FindForm(), "尚未取得角色清單，請先於系統設定驗證 Token。", "無法開新方案", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            if (route == null)
            {
                MessageBox.Show(this.FindForm(), "本機事件庫讀取失敗。", "無法開新方案", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var existingNames = _presetManager.LoadAll().Select(p => p.Name).ToHashSet();
            string baseName = "新方案";
            string newName = baseName;
            int counter = 2;
            while (existingNames.Contains(newName))
            {
                newName = $"{baseName} {counter++}";
            }

            var newPreset = new EventPreferencePreset
            {
                Name = newName,
                DeprioritizedCharacterIds = new List<string>()
            };

            using var editor = new EventPreferenceEditorForm(data.Characters, route, newPreset, existingNames);
            if (editor.ShowDialog(this.FindForm()) == DialogResult.OK)
            {
                _syncInitialPresetOnFirstProfileLoad = false;
                _presetManager.Save(newPreset);
                _activePreset = newPreset;
                AppSettingsManager.Current.ActiveEventPreferencePresetId = newPreset.Id;
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
            AppSettingsManager.Current.ActiveEventPreferencePresetId = copiedPreset.Id;
            AppSettingsManager.Save();

            RefreshPresetComboBox(copiedPreset.Id);
            UpdateSummaryLabel();
        }

        private void DeleteActivePreset()
        {
            var presets = _presetManager.LoadAll();
            if (presets.Count <= 1)
            {
                MessageBox.Show(this.FindForm(), "必須保留至少一個事件偏好方案，無法刪除。", "無法刪除", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var confirm = MessageBox.Show(this.FindForm(), $"確定要刪除事件偏好方案【{_activePreset.Name}】嗎？此動作無法復原。", "刪除確認", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (confirm != DialogResult.Yes) return;

            _presetManager.Delete(_activePreset.Id);

            presets = _presetManager.LoadAll();
            _activePreset = presets[0];
            AppSettingsManager.Current.ActiveEventPreferencePresetId = _activePreset.Id;
            AppSettingsManager.Save();

            RefreshPresetComboBox(_activePreset.Id);
            UpdateSummaryLabel();
        }

        private void ExportActivePreset()
        {
            if (_activePreset == null) return;

            using var sfd = new SaveFileDialog
            {
                Title = "匯出事件偏好方案",
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
                Title = "匯入事件偏好方案",
                Filter = "JSON 方案檔案 (*.json)|*.json"
            };

            if (ofd.ShowDialog(this.FindForm()) != DialogResult.OK) return;

            try
            {
                var imported = _presetManager.ImportFromFile(ofd.FileName);
                if (imported != null)
                {
                    _syncInitialPresetOnFirstProfileLoad = false;
                    _activePreset = imported;
                    AppSettingsManager.Current.ActiveEventPreferencePresetId = imported.Id;
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

        private async Task SubmitAsync()
        {
            if (_activePreset == null) return;

            _btnUpdate.Enabled = false;
            _lblSummary.ForeColor = Color.FromArgb(235, 185, 80);
            _lblSummary.Text = "⏳ 正在更新事件偏好至伺服器...";
            try
            {
                var res = await _service.UpdateAsync(_tokenProvider(), _activePreset.DeprioritizedCharacterIds);
                if (res.IsSuccess)
                {
                    UpdateSummaryLabel();
                    _lblSummary.Text = "✅ 已更新事件偏好。 " + _lblSummary.Text;
                    _lblSummary.ForeColor = Color.FromArgb(140, 200, 160);
                }
                else if (!res.IsCancelled)
                {
                    _lblSummary.Text = $"❌ 更新失敗：{res.ErrorMessage}";
                    _lblSummary.ForeColor = Color.FromArgb(235, 90, 90);
                }
            }
            finally
            {
                _btnUpdate.Enabled = true;
            }
        }

        private void UpdateSummaryLabel()
        {
            if (_lblSummary == null || _activePreset == null) return;

            var pendingIds = (_activePreset.DeprioritizedCharacterIds ?? new List<string>())
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Distinct()
                .ToList();
            var pending = new HashSet<string>(pendingIds);

            if (_lblSelectedNames != null)
            {
                if (pendingIds.Count == 0)
                {
                    _lblSelectedNames.Text = "減少所選角色的相關事件：無";
                }
                else
                {
                    var allChars = UserContext.Current?.CharactersData?.Characters;
                    if (allChars == null || allChars.Count == 0)
                    {
                        _lblSelectedNames.Text = $"減少所選角色的相關事件：{string.Join("、", pendingIds)}";
                    }
                    else
                    {
                        var dupNames = allChars
                            .GroupBy(c => c.Name)
                            .Where(g => g.Select(x => x.CharacterId).Distinct().Count() > 1)
                            .Select(g => g.Key)
                            .ToHashSet();

                        var nameById = new Dictionary<string, string>();
                        foreach (var c in allChars)
                        {
                            if (string.IsNullOrWhiteSpace(c.CharacterId) || nameById.ContainsKey(c.CharacterId)) continue;
                            nameById[c.CharacterId] = dupNames.Contains(c.Name) && !string.IsNullOrWhiteSpace(c.Title)
                                ? $"{c.Name}（{c.Title}）"
                                : c.Name;
                        }

                        var names = pendingIds.Select(id => nameById.TryGetValue(id, out var n) ? n : id);
                        _lblSelectedNames.Text = $"減少所選角色的相關事件：{string.Join("、", names)}";
                    }
                }
            }

            if (UserContext.Current?.CharactersData == null)
            {
                _lblSummary.ForeColor = Color.FromArgb(145, 155, 175);
                _lblSummary.Text = $"待更新：已選 {pending.Count} 位角色（尚未讀取伺服器狀態）";
                return;
            }

            var server = new HashSet<string>(
                ServerIds().Where(id => !string.IsNullOrWhiteSpace(id)));
            bool same = server.SetEquals(pending);

            _lblSummary.ForeColor = same ? Color.FromArgb(145, 155, 175) : Color.FromArgb(235, 185, 80);
            _lblSummary.Text = $"待更新：已選 {pending.Count} 位角色（伺服器目前 {server.Count} 位）｜" +
                               (same ? "與伺服器相同" : "尚未更新至伺服器");
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
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(ScaleDpi(8), ScaleDpi(4), ScaleDpi(3), ScaleDpi(4)),
                Margin = new Padding(0, ScaleDpi(1), ScaleDpi(6), ScaleDpi(4))
            };
            btn.FlatAppearance.BorderSize = 0;
            return btn;
        }
    }
}
