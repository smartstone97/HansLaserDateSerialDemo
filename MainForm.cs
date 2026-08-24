using System;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Reflection;

namespace HansLaserDateSerialDemo
{
    internal sealed class MainForm : Form
    {
        private const string ConfigFile = "config.json";
        private const string AuditFile = @".\mark-audit.csv";

        private readonly ToolStripMenuItem _settingsMenuItem;
        private readonly ToolStripMenuItem _viewMenuItem;
        private Label _codeValue;
        private Label _dateValue;
        private Label _serialValue;
        private Label _pendingWarning;
        private LogTextBox _log;
        private System.Windows.Forms.Timer _logAutoScrollResumeTimer;
        private bool _logAutoScrollPaused;
        private bool _suppressLogScrollTracking;
        private Button _previewButton;
        private Button _markButton;
        private Button _skipButton;
        private Button _exitButton;

        private AppConfiguration _configuration;
        private Product _product;
        private SequenceStore _store;
        private HansApi _api;
        private Reservation _reservation;
        private bool _busy;

        public MainForm()
        {
            Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
            Text = $@"{Resources.app_name} v{Assembly.GetExecutingAssembly().GetName().Version?.ToString(3)}";
            StartPosition = FormStartPosition.CenterScreen;
            MinimumSize = new Size(980, 680);
            Size = new Size(1080, 720);
            Font = new Font("Microsoft YaHei UI", 9F);

            TableLayoutPanel shell = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 2,
                Margin = Padding.Empty,
                Padding = Padding.Empty,
                BackColor = Color.White
            };
            shell.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            shell.RowStyles.Add(new RowStyle(SizeType.Absolute, 24F));
            shell.RowStyles.Add(new RowStyle(SizeType.Percent, 136F));
            Controls.Add(shell);

            MenuStrip menuStrip = new MenuStrip
            {
                Dock = DockStyle.Fill,
                AutoSize = true,
                Margin = Padding.Empty,
                Padding = new Padding(4, 2, 0, 2),
                BackColor = Color.White
            };
            _settingsMenuItem = new ToolStripMenuItem(Resources.setting)
            {
                Margin = Padding.Empty,
                Padding = new Padding(8, 0, 8, 0),
                ToolTipText = Resources.open_seetting
            };
            _settingsMenuItem.DropDownItems.Add(Resources.app_config, null,
                async delegate { await OpenSettingsAsync(SettingsPage.RunSettings); });
            _settingsMenuItem.DropDownItems.Add(Resources.prod_config, null,
                async delegate { await OpenSettingsAsync(SettingsPage.ProductConfiguration); });
            _settingsMenuItem.DropDownItems.Add(GetText("language"), null, delegate { OpenLanguageSelection(); });
            menuStrip.Items.Add(_settingsMenuItem);

            _viewMenuItem = new ToolStripMenuItem(Resources.view)
            {
                Margin = Padding.Empty,
                Padding = new Padding(8, 0, 8, 0),
                ToolTipText = Resources.view_record
            };
            _viewMenuItem.DropDownItems.Add(Resources.history, null, delegate { OpenHistory(); });
            menuStrip.Items.Add(_viewMenuItem);
            MainMenuStrip = menuStrip;

            shell.Controls.Add(menuStrip, 0, 0);

            Panel content = new Panel
            {
                Dock = DockStyle.Fill,
                Margin = Padding.Empty,
                Padding = new Padding(12)
            };
            shell.Controls.Add(content, 0, 1);
            content.Controls.Add(BuildOperationPanel());

            Load += async delegate
            {
                LoadConfiguration();
                if (_configuration.ProductId <= 0)
                {
                    return;
                }

                await StartWithSavedConfigurationWithRetryAsync();
            };
            FormClosing += delegate
            {
                DisposeLogAutoScrollTimer();
                DisposeApi();
            };
        }

        private Control BuildOperationPanel()
        {
            TableLayoutPanel root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 4,
                AutoSize = false
            };
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            GroupBox currentBox = new GroupBox
            {
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Dock = DockStyle.Fill,
                Margin = new Padding(0, 0, 0, 8),
                Text = Resources.current_num
            };
            root.Controls.Add(currentBox, 0, 0);

            FlowLayoutPanel currentFlow = new FlowLayoutPanel
            {
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Dock = DockStyle.Top,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                AutoScroll = false,
                Padding = new Padding(14, 12, 14, 10)
            };
            currentFlow.Resize += delegate
            {
                int rowWidth = currentFlow.ClientSize.Width - currentFlow.Padding.Left - currentFlow.Padding.Right;
                foreach (Control child in currentFlow.Controls)
                    child.Width = Math.Max(100, rowWidth);
            };
            currentBox.Controls.Add(currentFlow);

            _codeValue = AddValueRow(currentFlow, Resources.num, "--", 22F, true);
            _dateValue = AddValueRow(currentFlow, Resources.date, "--", 10F, false);
            _serialValue = AddValueRow(currentFlow, Resources.serial_num, "--", 10F, false);
            _pendingWarning = AddValueRow(currentFlow, Resources.state, Resources.state_init_message, 9F, false);
            _pendingWarning.ForeColor = Color.FromArgb(180, 96, 0);

            GroupBox flowBox = new GroupBox
            {
                Dock = DockStyle.Fill,
                Height = 140,
                Margin = new Padding(0, 0, 0, 8),
                Text = Resources.op_flow
            };
            root.Controls.Add(flowBox, 0, 1);

            TableLayoutPanel flow = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 4,
                RowCount = 1,
                Padding = new Padding(10)
            };
            for (int i = 0; i < 4; i++)
                flow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25F));
            flowBox.Controls.Add(flow);
            AddFlowStep(flow, 0, "1", Resources.flow_step_config_ready_title, Resources.flow_step_config_ready_text);
            AddFlowStep(flow, 1, "2", Resources.flow_step_number_ready_title, Resources.flow_step_number_ready_text);
            AddFlowStep(flow, 2, "3", Resources.flow_step_preview_mark_title, Resources.flow_step_preview_mark_text);
            AddFlowStep(flow, 3, "4", Resources.flow_step_confirm_exception_title, Resources.flow_step_confirm_exception_text);

            TableLayoutPanel actions = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                Height = 100,
                ColumnCount = 4,
                RowCount = 1,
                Padding = new Padding(0, 12, 0, 10)
            };
            for (int i = 0; i < 4; i++)
                actions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25F));
            root.Controls.Add(actions, 0, 2);

            _previewButton = AddActionButton(actions, 0, Resources.action_preview);
            _markButton = AddActionButton(actions, 1, Resources.action_mark);
            _skipButton = AddActionButton(actions, 2, Resources.action_skip);
            _exitButton = AddActionButton(actions, 3, Resources.action_exit);
            _previewButton.Click += async delegate { await PreviewAsync(); };
            _markButton.Click += async delegate { await MarkAsync(); };
            _skipButton.Click += delegate { SkipOrConfirm(); };
            _exitButton.Click += delegate { Close(); };

            GroupBox logBox = new GroupBox
            {
                Dock = DockStyle.Fill,
                Margin = new Padding(0),
                Text = Resources.run_log
            };
            root.Controls.Add(logBox, 0, 3);
            _log = new LogTextBox
            {
                Dock = DockStyle.Fill,
                Multiline = true,
                ScrollBars = ScrollBars.Vertical,
                ReadOnly = true,
                Font = new Font("Consolas", 9F),
                BackColor = Color.White
            };
            _log.UserScrollAction += delegate { HandleLogUserScrollAction(); };
            logBox.Controls.Add(_log);

            _logAutoScrollResumeTimer = new System.Windows.Forms.Timer { Interval = 30 * 1000 };
            _logAutoScrollResumeTimer.Tick += delegate
            {
                _logAutoScrollResumeTimer.Stop();
                ResumeLogAutoScroll();
            };

            UpdateActionButtons();
            return root;
        }

        private Label AddValueRow(FlowLayoutPanel container, string label, string value, float fontSize, bool bold)
        {
            Font valueFont = new Font(Font.FontFamily, fontSize, bold ? FontStyle.Bold : FontStyle.Regular);
            int rowHeight = Math.Max(28, TextRenderer.MeasureText(value, valueFont).Height + 8);

            TableLayoutPanel row = new TableLayoutPanel
            {
                AutoSize = false,
                Width = Math.Max(100, container.ClientSize.Width - container.Padding.Left - container.Padding.Right),
                Height = rowHeight,
                ColumnCount = 2,
                RowCount = 1,
                Margin = new Padding(0, 0, 0, 4),
                Padding = Padding.Empty
            };
            row.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 90));
            row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            row.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            container.Controls.Add(row);

            Label name = new Label
            {
                AutoSize = false,
                Dock = DockStyle.Fill,
                Margin = Padding.Empty,
                Text = label,
                TextAlign = ContentAlignment.MiddleLeft
            };
            row.Controls.Add(name, 0, 0);

            Label valueLabel = new Label
            {
                AutoSize = false,
                Dock = DockStyle.Fill,
                Margin = Padding.Empty,
                Text = value,
                TextAlign = ContentAlignment.MiddleLeft,
                Font = valueFont
            };
            row.Controls.Add(valueLabel, 1, 0);
            return valueLabel;
        }

        private static void AddFlowStep(TableLayoutPanel flow, int column, string number, string title, string text)
        {
            Panel panel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(8) };
            Label numberLabel = new Label
            {
                AutoSize = false,
                Width = 28,
                Height = 28,
                Text = number,
                TextAlign = ContentAlignment.MiddleCenter,
                BackColor = Color.FromArgb(229, 236, 246)
            };
            panel.Controls.Add(numberLabel);

            Label titleLabel = new Label
            {
                Left = 38,
                Top = 8,
                Width = 150,
                Height = 24,
                Text = title,
                Font = new Font("Microsoft YaHei UI", 9F, FontStyle.Bold)
            };
            panel.Controls.Add(titleLabel);

            Label textLabel = new Label
            {
                Left = 0,
                Top = 42,
                Width = 220,
                Height = 56,
                Text = text
            };
            panel.Controls.Add(textLabel);
            flow.Controls.Add(panel, column, 0);
        }

        private Button AddActionButton(TableLayoutPanel panel, int column, string text)
        {
            Button button = new Button
            {
                Dock = DockStyle.Fill,
                Margin = new Padding(6, 0, 6, 0),
                Text = text
            };
            panel.Controls.Add(button, column, 0);
            return button;
        }

        private void LoadConfiguration()
        {
            try
            {
                _configuration = LoadOrCreateConfiguration();
                Log(Resources.config_loaded_log);
            }
            catch (Exception ex)
            {
                Log(string.Format(Resources.config_load_failed_fmt, ex.Message));
            }
        }

        private async Task OpenSettingsAsync(SettingsPage initialPage)
        {
            if (_busy)
                return;

            AppConfiguration configuration;
            try
            {
                configuration = LoadOrCreateConfiguration();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, string.Format(Resources.config_load_failed_fmt, ex.Message), Resources.setting, MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            using SettingsDialog dialog = new SettingsDialog(configuration, initialPage);
            if (await dialog.ShowDialogAsync(this) != DialogResult.OK)
                return;

            await ApplyConfigurationAsync(dialog.Configuration, true);
        }

        private async Task StartWithSavedConfigurationWithRetryAsync()
        {
            while (!IsDisposed)
            {
                string errorMessage = await StartWithSavedConfigurationAsync();
                if (string.IsNullOrEmpty(errorMessage))
                    return;

                var messageBoxButtons =
                    errorMessage.Contains("dll") ? MessageBoxButtons.RetryCancel : MessageBoxButtons.OK;
                DialogResult result = MessageBox.Show(
                    this,
                    string.Format(Resources.startup_failed_fmt, errorMessage),
                    Resources.startup_title,
                    messageBoxButtons,
                    MessageBoxIcon.Error,
                    MessageBoxDefaultButton.Button1);

                if (result != DialogResult.Retry)
                    return;
            }
        }

        private async Task<string> StartWithSavedConfigurationAsync()
        {
            if (_busy)
                return Resources.busy_retry_message;

            AppConfiguration configuration;
            try
            {
                configuration = AppConfiguration.Load(ConfigFile);
            }
            catch (Exception ex)
            {
                Log(string.Format(Resources.saved_config_load_failed_fmt, ex.Message));
                return ex.Message;
            }

            return await ApplyConfigurationAsync(configuration, false);
        }

        private async Task<string> ApplyConfigurationAsync(AppConfiguration configuration, bool saveConfiguration)
        {
            return await RunBusyAsync(Resources.applying_config_status, delegate
            {
                if (saveConfiguration)
                    AppConfiguration.Save(ConfigFile, configuration);
                Product product = ResolveSelectedProduct(configuration);
                configuration.ValidateFiles();
                ValidateProductTemplate(product);

                Invoke(new Action(delegate
                {
                    DisposeApi();
                    ClearCurrentReservation(Resources.reapplying_config_message);
                }));

                HansApi newApi = new HansApi(configuration.DllPath);
                try
                {
                    newApi.Initialize(configuration.MachinePath);
                    newApi.LoadTemplate(product.TemplatePath);
                    string version = newApi.GetVersionText();

                    BeginInvoke(new Action(delegate
                    {
                        DisposeApi();
                        _api = newApi;
                        _configuration = configuration;
                        _product = product;
                        _store = new SequenceStore(
                            product,
                            CodeGeneratorFactory.Create(product.CodeGeneratorType, product.Pattern));
                        Log((saveConfiguration ? Resources.config_saved_applied_prefix : Resources.saved_config_started_prefix) + version);
                        ReserveAndDisplayCurrent();
                    }));
                }
                catch
                {
                    newApi.Dispose();
                    throw;
                }
            });
        }

        private void ClearCurrentReservation(string message)
        {
            _store = null;
            _product = null;
            _reservation = null;
            _codeValue.Text = "--";
            _dateValue.Text = "--";
            _serialValue.Text = "--";
            _pendingWarning.Text = message;
            UpdateActionButtons();
        }

        private AppConfiguration LoadOrCreateConfiguration()
        {
            string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ConfigFile);
            if (File.Exists(path))
                return AppConfiguration.Load(ConfigFile);

            AppConfiguration configuration = new AppConfiguration
            {
                MachinePath = @"C:\HansLaser\Marking",
                TemplatePath = @"C:\HansMark\Templates\DateSerial.HS",
                VariableTextAlias = "CODE",
                UseFootPedal = AppConfiguration.DefaultUseFootPedal,
                FootPedalTimeoutMs = AppConfiguration.DefaultFootPedalTimeoutMs,
                ProductId = AppConfiguration.DefaultProductId
            };
            AppConfiguration.Save(ConfigFile, configuration);
            return configuration;
        }

        private static Product ResolveSelectedProduct(AppConfiguration configuration)
        {
            using (AppDbContext dbContext = new AppDbContext())
            {
                ProductService productService = new ProductService(dbContext);
                Product product = configuration.ProductId > 0
                    ? productService.GetProduct(configuration.ProductId)
                    : null;

                if (product == null)
                    throw new InvalidOperationException(Resources.select_product_required);

                return product;
            }
        }

        private static void ValidateProductTemplate(Product product)
        {
            if (string.IsNullOrWhiteSpace(product.TemplatePath))
                throw new InvalidDataException(Resources.product_template_missing);

            if (!File.Exists(product.TemplatePath))
                throw new FileNotFoundException(Resources.product_template_not_found, product.TemplatePath);
        }

        private async Task PreviewAsync()
        {
            Reservation reservation = _reservation;
            if (reservation == null || _api == null)
                return;

            await RunBusyAsync(Resources.preview_busy, delegate
            {
                try
                {
                    MarkEndStatus status = _api.MarkAndWait(true, false, 0, 30 * 1000);
                    AuditLog.Append(AuditFile, "PREVIEW", reservation.Code, status.ToString());
                    BeginInvoke(new Action(delegate { Log(string.Format(Resources.preview_done_fmt, status)); }));
                }
                catch (Exception ex)
                {
                    AuditLog.Append(AuditFile, "PREVIEW_ERROR", reservation.Code, ex.Message);
                    BeginInvoke(new Action(delegate { Log(string.Format(Resources.preview_failed_fmt, ex.Message)); }));
                }
            });
        }

        private async Task MarkAsync()
        {
            Reservation reservation = _reservation;
            AppConfiguration configuration = _configuration;
            if (reservation == null || configuration == null || _api == null)
                return;

            string prompt = configuration.UseFootPedal
                ? Resources.mark_wait_foot
                : Resources.mark_now;

            await RunBusyAsync(prompt, delegate
            {
                try
                {
                    int overallTimeoutMs = configuration.UseFootPedal
                        ? configuration.FootPedalTimeoutMs + 60 * 1000
                        : 2 * 60 * 1000;

                    MarkEndStatus status = _api.MarkAndWait(
                        false,
                        configuration.UseFootPedal,
                        configuration.UseFootPedal ? configuration.FootPedalTimeoutMs : 0,
                        overallTimeoutMs);

                    uint? markTime = _api.TryGetLastMarkTimeMs();
                    string detail = status + (markTime.HasValue ? $"; {markTime.Value} ms" : string.Empty);

                    if (status == MarkEndStatus.Normal)
                    {
                        _store.Complete(reservation.Code);
                        AuditLog.Append(AuditFile, "MARK_SUCCESS", reservation.Code, detail);
                        BeginInvoke(new Action(delegate
                        {
                            Log(string.Format(Resources.mark_success_fmt, reservation.Code));
                            ReserveAndDisplayCurrent();
                        }));
                        return;
                    }

                    AuditLog.Append(AuditFile, "MARK_NOT_NORMAL", reservation.Code, detail);
                    BeginInvoke(new Action(delegate { Log(string.Format(Resources.mark_not_normal_fmt, status)); }));
                }
                catch (Exception ex)
                {
                    AuditLog.Append(AuditFile, "MARK_ERROR", reservation.Code, ex.Message);
                    BeginInvoke(new Action(delegate { Log(string.Format(Resources.mark_error_fmt, ex.Message)); }));
                }
            });
        }

        private void SkipOrConfirm()
        {
            if (_reservation == null || _store == null)
                return;

            DialogResult result = MessageBox.Show(
                this,
                string.Format(Resources.confirm_skip_message_fmt, _reservation.Code),
                Resources.confirm_skip_title,
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning,
                MessageBoxDefaultButton.Button2);

            if (result != DialogResult.Yes)
                return;

            _store.SkipOrConfirmAlreadyMarked(_reservation.Code);
            AuditLog.Append(AuditFile, "SKIP_OR_CONFIRMED", _reservation.Code, Resources.audit_skip_confirmed);
            Log(string.Format(Resources.skip_confirmed_log_fmt, _reservation.Code));
            ReserveAndDisplayCurrent();
        }

        private void OpenHistory()
        {
            using (MarkingRecordHistoryForm dialog = new MarkingRecordHistoryForm(
                       _configuration == null ? 0 : _configuration.ProductId,
                       ReprintRecordAsync))
            {
                dialog.ShowDialog(this);
            }
        }

        private void OpenLanguageSelection()
        {
            using (LanguageSelectionDialog dialog = new LanguageSelectionDialog(LanguageManager.CurrentCultureName))
            {
                if (dialog.ShowDialog(this) != DialogResult.OK)
                    return;

                if (string.Equals(dialog.SelectedCultureName, LanguageManager.CurrentCultureName, StringComparison.OrdinalIgnoreCase))
                    return;

                LanguageManager.SaveAndApply(dialog.SelectedCultureName);
                MessageBox.Show(this, GetText("language_restart_message"), GetText("language"), MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                Program.RequestRestart();
            }
        }

        private async Task ReprintRecordAsync(MarkingRecord source)
        {
            if (source == null)
                return;

            if (_api == null || _configuration == null || _product == null)
            {
                MessageBox.Show(this, Resources.reprint_missing_current_msg, Resources.reprint_title, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (source.ProductId != _product.Id)
            {
                MessageBox.Show(this, Resources.reprint_wrong_product_msg, Resources.reprint_title, MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return;
            }

            DialogResult result = MessageBox.Show(
                this,
                string.Format(Resources.reprint_confirm_fmt, source.Code),
                Resources.reprint_title,
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning,
                MessageBoxDefaultButton.Button2);
            if (result != DialogResult.Yes)
                return;

            await RunBusyAsync(Resources.reprint_busy, delegate
            {
                try
                {
                    _api.SetVariableText(_configuration.VariableTextAlias, source.Code);
                    int overallTimeoutMs = _configuration.UseFootPedal
                        ? _configuration.FootPedalTimeoutMs + 60 * 1000
                        : 2 * 60 * 1000;

                    MarkEndStatus status = _api.MarkAndWait(
                        false,
                        _configuration.UseFootPedal,
                        _configuration.UseFootPedal ? _configuration.FootPedalTimeoutMs : 0,
                        overallTimeoutMs);

                    uint? markTime = _api.TryGetLastMarkTimeMs();
                    string detail = status + (markTime.HasValue ? $"; {markTime.Value} ms" : string.Empty);

                    if (status == MarkEndStatus.Normal)
                    {
                        DateTime now = DateTime.Now;
                        using (AppDbContext dbContext = new AppDbContext())
                        {
                            dbContext.EnsureDatabase();
                            MarkingRecord record = dbContext.MarkingRecords.Single(item => item.Id == source.Id);
                            record.State = MarkingRecordStates.Reprinted;
                            record.MarkedAt = now;
                            record.UpdatedAt = now;
                            record.Remark = AppendRemark(record.Remark, Resources.reprint_remark);
                            dbContext.SaveChanges();
                        }

                        AuditLog.Append(AuditFile, "REPRINT_SUCCESS", source.Code, detail);
                        BeginInvoke(new Action(delegate { Log(string.Format(Resources.reprint_success_fmt, source.Code)); }));
                    }
                    else
                    {
                        AuditLog.Append(AuditFile, "REPRINT_NOT_NORMAL", source.Code, detail);
                        BeginInvoke(new Action(delegate { Log(string.Format(Resources.reprint_not_normal_fmt, status)); }));
                    }
                }
                catch (Exception ex)
                {
                    AuditLog.Append(AuditFile, "REPRINT_ERROR", source.Code, ex.Message);
                    BeginInvoke(new Action(delegate { Log(string.Format(Resources.reprint_error_fmt, ex.Message)); }));
                }
                finally
                {
                    if (_reservation != null)
                        _api.SetVariableText(_configuration.VariableTextAlias, _reservation.Code);
                }
            });
        }

        private static string AppendRemark(string currentRemark, string newRemark)
        {
            string timestampedRemark = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} {newRemark}";
            if (string.IsNullOrWhiteSpace(currentRemark))
                return timestampedRemark;

            return currentRemark + Environment.NewLine + timestampedRemark;
        }

        private void ReserveAndDisplayCurrent()
        {
            try
            {
                _reservation = _store.GetOrReserve(DateTime.Now);
                _api.SetVariableText(_configuration.VariableTextAlias, _reservation.Code);
                AuditLog.Append(
                    AuditFile,
                    _reservation.WasAlreadyPending ? "RESUME_PENDING" : "RESERVE",
                    _reservation.Code,
                    $"{_reservation.Date:yyyy-MM-dd}/{_reservation.Serial}");

                _codeValue.Text = _reservation.Code;
                _dateValue.Text = _reservation.Date.ToString("yyyy-MM-dd");
                _serialValue.Text = _reservation.Serial.ToString("0000");
                _pendingWarning.Text = _reservation.WasAlreadyPending
                    ? Resources.pending_previous_message
                    : Resources.pending_new_message;
            }
            catch (Exception ex)
            {
                Log(string.Format(Resources.reserve_failed_fmt, ex.Message));
            }
            finally
            {
                UpdateActionButtons();
            }
        }

        private async Task<string> RunBusyAsync(string status, Action action)
        {
            _busy = true;
            UpdateActionButtons();

            try
            {
                await Task.Run(action);
                return null;
            }
            catch (Exception ex)
            {
                Log(string.Format(Resources.operation_failed_fmt, ex.Message));
                return ex.Message;
            }
            finally
            {
                _busy = false;
                UpdateActionButtons();
            }
        }

        private void UpdateActionButtons()
        {
            bool ready = !_busy && _api != null && _reservation != null;
            _settingsMenuItem.Enabled = !_busy;
            _viewMenuItem.Enabled = !_busy;
            _previewButton.Enabled = ready;
            _markButton.Enabled = ready;
            _skipButton.Enabled = ready;
            _exitButton.Enabled = !_busy;
        }

        private void Log(string message)
        {
            bool shouldAutoScroll = !_logAutoScrollPaused || _log.IsScrolledToBottom();

            _suppressLogScrollTracking = true;
            try
            {
                _log.AppendLogText($"{DateTime.Now:HH:mm:ss}  {message}{Environment.NewLine}", shouldAutoScroll);
            }
            finally
            {
                _suppressLogScrollTracking = false;
            }

            if (shouldAutoScroll)
                ResumeLogAutoScroll();
        }

        private void HandleLogUserScrollAction()
        {
            if (_suppressLogScrollTracking)
                return;

            if (_log.IsScrolledToBottom())
            {
                ResumeLogAutoScroll();
                return;
            }

            _logAutoScrollPaused = true;
            _logAutoScrollResumeTimer.Stop();
            _logAutoScrollResumeTimer.Start();
        }

        private void ResumeLogAutoScroll()
        {
            _logAutoScrollPaused = false;
            _logAutoScrollResumeTimer.Stop();
            _suppressLogScrollTracking = true;
            try
            {
                _log.ScrollToBottom();
            }
            finally
            {
                _suppressLogScrollTracking = false;
            }
        }

        private static string GetText(string key)
        {
            return Resources.ResourceManager.GetString(key, Resources.Culture) ?? key;
        }

        private void DisposeApi()
        {
            if (_api != null)
            {
                _api.Dispose();
                _api = null;
            }
        }

        private void DisposeLogAutoScrollTimer()
        {
            if (_logAutoScrollResumeTimer == null)
                return;

            _logAutoScrollResumeTimer.Stop();
            _logAutoScrollResumeTimer.Dispose();
            _logAutoScrollResumeTimer = null;
        }

        private sealed class LogTextBox : TextBox
        {
            private const int EmGetFirstVisibleLine = 0x00CE;
            private const int EmLineScroll = 0x00B6;
            private const int WmVScroll = 0x0115;
            private const int WmMouseWheel = 0x020A;
            private const int SbVert = 1;
            private const uint SifRange = 0x0001;
            private const uint SifPage = 0x0002;
            private const uint SifPos = 0x0004;

            public event EventHandler UserScrollAction;

            public bool IsScrolledToBottom()
            {
                if (!IsHandleCreated)
                    return true;

                ScrollInfo info = new ScrollInfo
                {
                    cbSize = Marshal.SizeOf(typeof(ScrollInfo)),
                    fMask = SifRange | SifPage | SifPos
                };

                if (!GetScrollInfo(Handle, SbVert, ref info))
                    return true;

                return info.nPos + Math.Max(1, info.nPage) >= info.nMax;
            }

            public void AppendLogText(string text, bool scrollToBottom)
            {
                if (scrollToBottom)
                {
                    AppendText(text);
                    ScrollToBottom();
                    return;
                }

                int firstVisibleLine = GetFirstVisibleLine();
                int selectionStart = SelectionStart;
                int selectionLength = SelectionLength;

                AppendText(text);

                SelectionStart = Math.Min(selectionStart, TextLength);
                SelectionLength = Math.Min(selectionLength, TextLength - SelectionStart);
                ScrollToLine(firstVisibleLine);
            }

            public void ScrollToBottom()
            {
                SelectionStart = TextLength;
                SelectionLength = 0;
                ScrollToCaret();
            }

            protected override void WndProc(ref Message m)
            {
                bool userScrollMessage = m.Msg == WmVScroll || m.Msg == WmMouseWheel;
                base.WndProc(ref m);

                if (userScrollMessage)
                    OnUserScrollAction();
            }

            protected override void OnKeyDown(KeyEventArgs e)
            {
                base.OnKeyDown(e);
                if (IsScrollKey(e))
                    OnUserScrollAction();
            }

            protected override void OnMouseUp(MouseEventArgs e)
            {
                base.OnMouseUp(e);
                OnUserScrollAction();
            }

            private int GetFirstVisibleLine()
            {
                return IsHandleCreated ? SendMessage(Handle, EmGetFirstVisibleLine, IntPtr.Zero, IntPtr.Zero).ToInt32() : 0;
            }

            private void ScrollToLine(int line)
            {
                int currentFirstLine = GetFirstVisibleLine();
                SendMessage(Handle, EmLineScroll, IntPtr.Zero, new IntPtr(line - currentFirstLine));
            }

            private void OnUserScrollAction()
            {
                UserScrollAction?.Invoke(this, EventArgs.Empty);
            }

            private static bool IsScrollKey(KeyEventArgs e)
            {
                return e.KeyCode == Keys.Up ||
                       e.KeyCode == Keys.Down ||
                       e.KeyCode == Keys.PageUp ||
                       e.KeyCode == Keys.PageDown ||
                       e.KeyCode == Keys.Home ||
                       e.KeyCode == Keys.End;
            }

            [DllImport("user32.dll")]
            private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

            [DllImport("user32.dll", SetLastError = true)]
            private static extern bool GetScrollInfo(IntPtr hwnd, int nBar, ref ScrollInfo lpsi);

            [StructLayout(LayoutKind.Sequential)]
            private struct ScrollInfo
            {
                public int cbSize;
                public uint fMask;
                public int nMin;
                public int nMax;
                public uint nPage;
                public int nPos;
                public int nTrackPos;
            }
        }
    }
}
