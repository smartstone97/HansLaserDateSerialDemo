using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.EntityFrameworkCore;

namespace HansLaserDateSerialDemo
{
    internal sealed class MarkingRecordHistoryForm : Form
    {
        private readonly ComboBox _productComboBox;
        private readonly ComboBox _stateComboBox;
        private readonly DateTimePicker _fromDatePicker;
        private readonly DateTimePicker _toDatePicker;
        private readonly TextBox _keywordTextBox;
        private readonly DataGridView _recordsGrid;
        private readonly Button _reprintButton;
        private readonly Func<MarkingRecord, Task> _reprintHandler;

        private List<Product> _products = new List<Product>();

        public MarkingRecordHistoryForm(int selectedProductId, Func<MarkingRecord, Task> reprintHandler)
        {
            _reprintHandler = reprintHandler ?? throw new ArgumentNullException(nameof(reprintHandler));

            Text = Resources.history_marking_records;
            StartPosition = FormStartPosition.CenterParent;
            MinimumSize = new Size(920, 560);
            Size = new Size(1040, 660);
            Font = new Font("Microsoft YaHei UI", 9F);

            TableLayoutPanel shell = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 3,
                Padding = new Padding(12)
            };
            shell.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
            shell.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            shell.RowStyles.Add(new RowStyle(SizeType.Absolute, 52));
            Controls.Add(shell);

            TableLayoutPanel filters = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 10,
                RowCount = 1
            };
            filters.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 52));
            filters.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 190));
            filters.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 52));
            filters.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 116));
            filters.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 52));
            filters.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 116));
            filters.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 52));
            filters.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 110));
            filters.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 64));
            filters.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            shell.Controls.Add(filters, 0, 0);

            AddLabel(filters, 0, Resources.product);
            _productComboBox = new ComboBox { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList, Margin = new Padding(0, 8, 8, 8) };
            filters.Controls.Add(_productComboBox, 1, 0);

            AddLabel(filters, 2, Resources.start);
            _fromDatePicker = new DateTimePicker { Dock = DockStyle.Fill, Format = DateTimePickerFormat.Short, Margin = new Padding(0, 8, 8, 8) };
            filters.Controls.Add(_fromDatePicker, 3, 0);

            AddLabel(filters, 4, Resources.end);
            _toDatePicker = new DateTimePicker { Dock = DockStyle.Fill, Format = DateTimePickerFormat.Short, Margin = new Padding(0, 8, 8, 8) };
            filters.Controls.Add(_toDatePicker, 5, 0);

            AddLabel(filters, 6, Resources.state);
            _stateComboBox = new ComboBox { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList, Margin = new Padding(0, 8, 8, 8) };
            _stateComboBox.Items.Add(Resources.all);
            _stateComboBox.Items.Add(MarkingRecordStates.Pending);
            _stateComboBox.Items.Add(MarkingRecordStates.Marked);
            _stateComboBox.Items.Add(MarkingRecordStates.Skipped);
            _stateComboBox.Items.Add(MarkingRecordStates.Reprinted);
            _stateComboBox.SelectedIndex = 0;
            filters.Controls.Add(_stateComboBox, 7, 0);

            AddLabel(filters, 8, Resources.num);
            _keywordTextBox = new TextBox { Dock = DockStyle.Fill, Margin = new Padding(0, 9, 0, 8) };
            filters.Controls.Add(_keywordTextBox, 9, 0);

            _recordsGrid = new DataGridView
            {
                Dock = DockStyle.Fill,
                AutoGenerateColumns = false,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                MultiSelect = false,
                ReadOnly = true,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                RowHeadersVisible = false
            };
            _recordsGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = Resources.num, DataPropertyName = "Code", Width = 180 });
            _recordsGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = Resources.product, DataPropertyName = "ProductName", Width = 160 });
            _recordsGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = Resources.customer_part_number, DataPropertyName = "CustomerPartNumber", Width = 130 });
            _recordsGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = Resources.date, DataPropertyName = "BusinessDate", Width = 100 });
            _recordsGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = Resources.serial_num, DataPropertyName = "Serial", Width = 80 });
            _recordsGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = Resources.state, DataPropertyName = "State", Width = 100 });
            _recordsGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = Resources.created_at, DataPropertyName = "CreatedAt", Width = 150 });
            _recordsGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = Resources.marked_at, DataPropertyName = "MarkedAt", Width = 150 });
            _recordsGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = Resources.remark, DataPropertyName = "Remark", AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill });
            _recordsGrid.SelectionChanged += delegate { UpdateButtons(); };
            shell.Controls.Add(_recordsGrid, 0, 1);

            FlowLayoutPanel buttons = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.RightToLeft,
                WrapContents = false,
                Padding = new Padding(0, 10, 0, 0)
            };
            shell.Controls.Add(buttons, 0, 2);

            Button closeButton = new Button { Width = 90, Height = 32, Text = Resources.close };
            closeButton.Click += delegate { Close(); };
            buttons.Controls.Add(closeButton);

            _reprintButton = new Button { Width = 110, Height = 32, Text = Resources.reprint };
            _reprintButton.Click += async delegate { await ReprintSelectedAsync(); };
            buttons.Controls.Add(_reprintButton);

            Button searchButton = new Button { Width = 90, Height = 32, Text = Resources.search };
            searchButton.Click += delegate { RefreshRecords(); };
            buttons.Controls.Add(searchButton);

            _productComboBox.SelectedIndexChanged += delegate { RefreshRecords(); };
            _stateComboBox.SelectedIndexChanged += delegate { RefreshRecords(); };
            _keywordTextBox.KeyDown += delegate(object sender, KeyEventArgs e)
            {
                if (e.KeyCode == Keys.Enter)
                {
                    RefreshRecords();
                    e.Handled = true;
                    e.SuppressKeyPress = true;
                }
            };

            Load += delegate
            {
                _fromDatePicker.Value = DateTime.Today.AddDays(-30);
                _toDatePicker.Value = DateTime.Today;
                LoadProducts(selectedProductId);
                RefreshRecords();
            };
        }

        private static void AddLabel(TableLayoutPanel panel, int column, string text)
        {
            panel.Controls.Add(new Label
            {
                Dock = DockStyle.Fill,
                Text = text,
                TextAlign = ContentAlignment.MiddleLeft
            }, column, 0);
        }

        private void LoadProducts(int selectedProductId)
        {
            using (AppDbContext dbContext = new AppDbContext())
            {
                dbContext.EnsureDatabase();
                _products = dbContext.Products
                    .OrderBy(product => product.Name)
                    .ThenBy(product => product.CustomerPartNumber)
                    .ToList();
            }

            _productComboBox.BeginUpdate();
            _productComboBox.Items.Clear();
            _productComboBox.Items.Add(new Selection<Product>(Resources.all, null));
            foreach (Product product in _products)
                _productComboBox.Items.Add(new Selection<Product>(BuildProductLabel(product), product));
            _productComboBox.EndUpdate();

            _productComboBox.SelectedIndex = 0;
            if (selectedProductId > 0)
            {
                for (int i = 1; i < _productComboBox.Items.Count; i++)
                {
                    Selection<Product> selection = (Selection<Product>)_productComboBox.Items[i];
                    if (selection.Value.Id == selectedProductId)
                    {
                        _productComboBox.SelectedIndex = i;
                        break;
                    }
                }
            }
        }

        private void RefreshRecords()
        {
            using (AppDbContext dbContext = new AppDbContext())
            {
                dbContext.EnsureDatabase();
                IQueryable<MarkingRecord> query = dbContext.MarkingRecords
                    .Include(record => record.Product)
                    .AsNoTracking();

                Selection<Product> productSelection = _productComboBox.SelectedItem as Selection<Product>;
                if (productSelection != null && productSelection.Value != null)
                {
                    int productId = productSelection.Value.Id;
                    query = query.Where(record => record.ProductId == productId);
                }

                DateTime from = _fromDatePicker.Value.Date;
                DateTime toExclusive = _toDatePicker.Value.Date.AddDays(1);
                query = query.Where(record => record.BusinessDate >= from && record.BusinessDate < toExclusive);

                string state = _stateComboBox.SelectedItem == null ? Resources.all : _stateComboBox.SelectedItem.ToString();
                if (!string.Equals(state, Resources.all, StringComparison.Ordinal))
                    query = query.Where(record => record.State == state);

                string keyword = _keywordTextBox.Text.Trim();
                if (keyword.Length > 0)
                    query = query.Where(record => record.Code.Contains(keyword));

                List<MarkingRecord> records = query
                    .OrderByDescending(record => record.CreatedAt)
                    .Take(1000)
                    .ToList();

                List<RecordRow> rows = records
                    .Select(record => new RecordRow
                    {
                        Record = record,
                        Code = record.Code,
                        ProductName = record.Product == null ? "" : record.Product.Name,
                        CustomerPartNumber = record.Product == null ? "" : record.Product.CustomerPartNumber,
                        BusinessDate = record.BusinessDate.ToString("yyyy-MM-dd"),
                        Serial = record.Serial.ToString("0000"),
                        State = record.State,
                        CreatedAt = FormatDateTime(record.CreatedAt),
                        MarkedAt = record.MarkedAt.HasValue ? FormatDateTime(record.MarkedAt.Value) : "",
                        Remark = record.Remark
                    })
                    .ToList();

                _recordsGrid.DataSource = rows;
            }

            UpdateButtons();
        }

        private async Task ReprintSelectedAsync()
        {
            RecordRow row = GetSelectedRow();
            if (row == null)
                return;

            await _reprintHandler(row.Record);
            RefreshRecords();
        }

        private RecordRow GetSelectedRow()
        {
            if (_recordsGrid.CurrentRow == null)
                return null;

            return _recordsGrid.CurrentRow.DataBoundItem as RecordRow;
        }

        private void UpdateButtons()
        {
            _reprintButton.Enabled = GetSelectedRow() != null;
        }

        private static string BuildProductLabel(Product product)
        {
            string part = string.IsNullOrWhiteSpace(product.CustomerPartNumber) ? "-" : product.CustomerPartNumber;
            return $"{product.Name} [{part}]";
        }

        private static string FormatDateTime(DateTime value)
        {
            return value.Year <= 1 ? "" : value.ToString("yyyy-MM-dd HH:mm:ss");
        }

        private sealed class RecordRow
        {
            public MarkingRecord Record { get; set; }
            public string Code { get; set; }
            public string ProductName { get; set; }
            public string CustomerPartNumber { get; set; }
            public string BusinessDate { get; set; }
            public string Serial { get; set; }
            public string State { get; set; }
            public string CreatedAt { get; set; }
            public string MarkedAt { get; set; }
            public string Remark { get; set; }
        }
    }
}
