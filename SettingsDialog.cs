using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace HansLaserDateSerialDemo
{
    internal enum SettingsPage
    {
        RunSettings,
        ProductConfiguration
    }

    internal sealed class SettingsDialog : Form
    {
        private readonly TextBox _machinePathTextBox;
        private readonly TextBox _variableTextAliasTextBox;
        private readonly ComboBox _productComboBox;
        private readonly CheckBox _useFootPedal;
        private readonly NumericUpDown _footPedalTimeoutSeconds;
        private readonly Label _dllVersionLabel;
        private readonly DataGridView _productsGrid;
        private readonly Button _editProductButton;
        private readonly Button _deleteProductButton;
        private readonly SettingsPage _initialPage;

        private List<Product> _products = new List<Product>();

        public AppConfiguration Configuration { get; private set; }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            base.OnFormClosing(e);
            SaveAndClose();
        }

        public SettingsDialog(AppConfiguration configuration, SettingsPage initialPage)
        {
            if (configuration == null)
                throw new ArgumentNullException(nameof(configuration));

            _initialPage = initialPage;
            Text = initialPage == SettingsPage.ProductConfiguration ? Resources.prod_config : Resources.app_config;
            StartPosition = FormStartPosition.CenterParent;
            MinimumSize = new Size(760, 620);
            Size = new Size(860, 700);
            Font = new Font("Microsoft YaHei UI", 9F);

            TableLayoutPanel shell = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 2,
                Padding = new Padding(14)
            };
            shell.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            shell.RowStyles.Add(new RowStyle(SizeType.Absolute,
                initialPage == SettingsPage.ProductConfiguration ? 0 : 56));
            Controls.Add(shell);

            FlowLayoutPanel settingsRoot = CreateVerticalFlow();

            GroupBox basicBox = AddGroup(settingsRoot, Resources.basic_config, 192);
            TableLayoutPanel basicGrid = CreateFormGrid(3);
            basicBox.Controls.Add(basicGrid);

            _machinePathTextBox = AddPathSettingTextBox(
                basicGrid,
                0,
                Resources.device_config_dir,
                delegate(TextBox textBox) { BrowseFolder(textBox, Resources.browse_device_config_dir); });
            _dllVersionLabel = AddDllVersionRow(basicGrid, 1);
            _variableTextAliasTextBox = AddSettingTextBox(basicGrid, 2, Resources.variable_text_alias);

            GroupBox generatorBox = AddGroup(settingsRoot, Resources.product, 82);
            TableLayoutPanel generatorGrid = CreateFormGrid(1);
            generatorBox.Controls.Add(generatorGrid);

            _productComboBox = AddComboBox(generatorGrid, 0, Resources.select_product);

            GroupBox pedalBox = AddGroup(settingsRoot, Resources.foot_pedal_trigger, 104);
            TableLayoutPanel pedalGrid = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 4,
                RowCount = 1,
                Padding = new Padding(12)
            };
            pedalGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 180));
            pedalGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120));
            pedalGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120));
            pedalGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            pedalBox.Controls.Add(pedalGrid);

            _useFootPedal = new CheckBox
            {
                Dock = DockStyle.Fill,
                Text = Resources.enable_foot_pedal
            };
            pedalGrid.Controls.Add(_useFootPedal, 0, 0);

            Label timeoutLabel = new Label
            {
                Dock = DockStyle.Fill,
                Text = Resources.timeout,
                TextAlign = ContentAlignment.MiddleLeft
            };
            pedalGrid.Controls.Add(timeoutLabel, 1, 0);

            _footPedalTimeoutSeconds = new NumericUpDown
            {
                Dock = DockStyle.Fill,
                Minimum = 1,
                Maximum = 3600,
                Increment = 5,
                Margin = new Padding(0, 12, 8, 0)
            };
            pedalGrid.Controls.Add(_footPedalTimeoutSeconds, 2, 0);

            Label secondsLabel = new Label
            {
                Dock = DockStyle.Fill,
                Text = Resources.seconds,
                TextAlign = ContentAlignment.MiddleLeft
            };
            pedalGrid.Controls.Add(secondsLabel, 3, 0);

            TableLayoutPanel productsRoot = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 2,
                Padding = new Padding(4)
            };
            productsRoot.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            productsRoot.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));

            _productsGrid = new DataGridView
            {
                Dock = DockStyle.Fill,
                AutoGenerateColumns = false,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                MultiSelect = false,
                ReadOnly = true,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                RowHeadersVisible = false,
                ColumnHeadersHeight = 28,
                ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing
            };
            _productsGrid.Columns.Add(new DataGridViewTextBoxColumn
                { HeaderText = Resources.name, DataPropertyName = "Name", Width = 150 });
            _productsGrid.Columns.Add(new DataGridViewTextBoxColumn
                { HeaderText = Resources.customer_part_number, DataPropertyName = "CustomerPartNumber", Width = 150 });
            _productsGrid.Columns.Add(new DataGridViewTextBoxColumn
                { HeaderText = "Shipcode", DataPropertyName = "Shipcode", Width = 90 });
            _productsGrid.Columns.Add(new DataGridViewTextBoxColumn
                { HeaderText = Resources.serial_start, DataPropertyName = "SerialStartValue", Width = 90 });
            _productsGrid.Columns.Add(new DataGridViewTextBoxColumn
                { HeaderText = Resources.generator, DataPropertyName = "CodeGeneratorType", Width = 100 });
            _productsGrid.Columns.Add(new DataGridViewTextBoxColumn
            {
                HeaderText = Resources.template, DataPropertyName = "TemplatePath",
                AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill, MinimumWidth = 150
            });
            _productsGrid.Columns.Add(new DataGridViewTextBoxColumn
                { HeaderText = "Pattern", DataPropertyName = "Pattern", Width = 100 });
            _productsGrid.CellDoubleClick += delegate(object sender, DataGridViewCellEventArgs e)
            {
                if (e.RowIndex >= 0)
                    OpenProductEditorAtRow(e.RowIndex);
            };
            _productsGrid.SelectionChanged += delegate { UpdateProductActionButtons(); };
            _productsGrid.DataError += delegate(object sender, DataGridViewDataErrorEventArgs e)
            {
                e.ThrowException = false;
            };
            productsRoot.Controls.Add(_productsGrid, 0, 0);

            FlowLayoutPanel productButtons = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.RightToLeft,
                WrapContents = false,
                Margin = Padding.Empty,
                Padding = new Padding(0, 8, 0, 0)
            };
            productsRoot.Controls.Add(productButtons, 0, 1);

            _deleteProductButton = new Button { Width = 90, Height = 30, Text = Resources.delete };
            _deleteProductButton.Click += delegate { DeleteSelectedProduct(); };
            productButtons.Controls.Add(_deleteProductButton);

            _editProductButton = new Button { Width = 90, Height = 30, Text = Resources.edit };
            _editProductButton.Click += delegate { OpenSelectedProductEditor(); };
            productButtons.Controls.Add(_editProductButton);

            Button addProductButton = new Button { Width = 90, Height = 30, Text = Resources.new_product };
            addProductButton.Click += delegate { OpenProductEditor(null); };
            productButtons.Controls.Add(addProductButton);

            FlowLayoutPanel buttons = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.RightToLeft,
                Margin = Padding.Empty,
                Padding = new Padding(0, 10, 0, 0),
                WrapContents = false
            };
            shell.Controls.Add(buttons, 0, 1);

            LoadProducts(configuration.ProductId);
            ShowConfiguration(configuration);
            shell.Controls.Add(initialPage == SettingsPage.ProductConfiguration
                ? (Control)productsRoot
                : settingsRoot, 0, 0);
        }

        private static FlowLayoutPanel CreateVerticalFlow()
        {
            FlowLayoutPanel root = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                Margin = Padding.Empty,
                Padding = new Padding(10)
            };
            root.Resize += delegate
            {
                int width = root.ClientSize.Width - root.Padding.Left - root.Padding.Right;
                foreach (Control child in root.Controls)
                    child.Width = Math.Max(300, width - SystemInformation.VerticalScrollBarWidth);
            };
            return root;
        }

        private static GroupBox AddGroup(FlowLayoutPanel root, string text, int height)
        {
            GroupBox box = new GroupBox
            {
                Width = 700,
                Height = height,
                Margin = new Padding(0, 0, 0, 10),
                Text = text
            };
            root.Controls.Add(box);
            return box;
        }

        private static TableLayoutPanel CreateFormGrid(int rows)
        {
            TableLayoutPanel grid = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = rows,
                Padding = new Padding(12)
            };
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150));
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            for (int i = 0; i < rows; i++)
                grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
            return grid;
        }

        private TextBox AddSettingTextBox(TableLayoutPanel grid, int row, string label)
        {
            AddLabel(grid, row, label);
            TextBox textBox = new TextBox
            {
                Dock = DockStyle.Fill,
                Margin = new Padding(0, 8, 0, 0)
            };
            grid.Controls.Add(textBox, 1, row);
            return textBox;
        }

        private TextBox AddReadOnlyTextBox(TableLayoutPanel grid, int row, string label)
        {
            TextBox textBox = AddSettingTextBox(grid, row, label);
            textBox.ReadOnly = true;
            textBox.BackColor = Color.White;
            return textBox;
        }

        private ComboBox AddComboBox(TableLayoutPanel grid, int row, string label)
        {
            AddLabel(grid, row, label);
            ComboBox comboBox = new ComboBox
            {
                Dock = DockStyle.Fill,
                DropDownStyle = ComboBoxStyle.DropDownList,
                Margin = new Padding(0, 8, 0, 0)
            };
            grid.Controls.Add(comboBox, 1, row);
            return comboBox;
        }

        private NumericUpDown AddNumericSetting(TableLayoutPanel grid, int row, string label)
        {
            AddLabel(grid, row, label);
            NumericUpDown numeric = new NumericUpDown
            {
                Dock = DockStyle.Left,
                Minimum = 0,
                Maximum = 999999,
                Width = 140,
                Margin = new Padding(0, 8, 0, 0)
            };
            grid.Controls.Add(numeric, 1, row);
            return numeric;
        }

        private void AddLabel(TableLayoutPanel grid, int row, string label)
        {
            Label name = new Label
            {
                Dock = DockStyle.Fill,
                Text = label,
                TextAlign = ContentAlignment.MiddleLeft
            };
            grid.Controls.Add(name, 0, row);
        }

        private TextBox AddPathSettingTextBox(TableLayoutPanel grid, int row, string label,
            Action<TextBox> browseAction)
        {
            AddLabel(grid, row, label);

            TableLayoutPanel panel = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                Margin = Padding.Empty
            };
            panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 78));
            grid.Controls.Add(panel, 1, row);

            TextBox textBox = new TextBox
            {
                Dock = DockStyle.Fill,
                Margin = new Padding(0, 8, 8, 0)
            };
            panel.Controls.Add(textBox, 0, 0);

            Button browseButton = new Button
            {
                Dock = DockStyle.Fill,
                Margin = new Padding(0, 6, 0, 4),
                Text = Resources.open
            };
            browseButton.Click += delegate { browseAction(textBox); };
            panel.Controls.Add(browseButton, 1, 0);
            return textBox;
        }

        private Label AddDllVersionRow(TableLayoutPanel grid, int row)
        {
            AddLabel(grid, row, Resources.dll_version);

            TableLayoutPanel panel = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                Margin = Padding.Empty
            };
            panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 116));
            panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            grid.Controls.Add(panel, 1, row);

            Button readButton = new Button
            {
                Dock = DockStyle.Fill,
                Margin = new Padding(0, 6, 8, 4),
                Text = Resources.read_version
            };
            readButton.Click += delegate { ReadDllVersion(); };
            panel.Controls.Add(readButton, 0, 0);

            Label versionLabel = new Label
            {
                Dock = DockStyle.Fill,
                AutoEllipsis = true,
                TextAlign = ContentAlignment.MiddleLeft,
                Text = Resources.not_read
            };
            panel.Controls.Add(versionLabel, 1, 0);
            return versionLabel;
        }

        private void LoadProducts(int selectedProductId)
        {
            using (AppDbContext dbContext = new AppDbContext())
            {
                ProductService productService = new ProductService(dbContext);
                _products = productService.GetProducts();
            }

            RefreshProductComboBox(selectedProductId);
            RefreshProductsGrid();
        }

        private void RefreshProductComboBox(int selectedProductId)
        {
            _productComboBox.BeginUpdate();
            _productComboBox.Items.Clear();
            foreach (Product product in _products)
            {
                if (product.Id > 0)
                    _productComboBox.Items.Add(new Selection<Product>(BuildProductLabel(product), product));
            }

            _productComboBox.EndUpdate();
            SelectProduct(selectedProductId);
        }

        private void RefreshProductsGrid()
        {
            _productsGrid.DataSource = null;
            _productsGrid.DataSource = _products;
            if (_products.Count == 0)
                _productsGrid.ClearSelection();
            UpdateProductActionButtons();
        }

        private void UpdateProductActionButtons()
        {
            bool hasSelection = GetSelectedProductRowIndex() >= 0;
            _editProductButton.Enabled = hasSelection;
            _deleteProductButton.Enabled = hasSelection;
        }

        private void OpenProductEditorAtRow(int rowIndex)
        {
            if (rowIndex < 0 || rowIndex >= _products.Count)
                return;

            OpenProductEditor(_products[rowIndex]);
        }

        private void OpenSelectedProductEditor()
        {
            int rowIndex = GetSelectedProductRowIndex();
            if (rowIndex >= 0)
                OpenProductEditorAtRow(rowIndex);
        }

        private void OpenProductEditor(Product source)
        {
            Product draft = source == null ? new Product() : CopyProduct(source);
            using (ProductEditorDialog dialog = new ProductEditorDialog(draft))
            {
                if (dialog.ShowDialog(this) != DialogResult.OK)
                    return;

                SaveProduct(dialog.Product);
            }
        }

        private void DeleteProductAtRow(int rowIndex)
        {
            if (rowIndex < 0 || rowIndex >= _products.Count)
                return;

            Product product = _products[rowIndex];
            if (product == null)
                return;

            if (product.Id <= 0)
            {
                RemoveProductFromGrid(product, 0);
                return;
            }

            if (product.Id > 0)
            {
                DialogResult result = MessageBox.Show(
                    GetRootOwner(this),
                    string.Format(Resources.confirm_delete_product_fmt, product.Name),
                    Resources.prod_config,
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Warning,
                    MessageBoxDefaultButton.Button2);
                if (result != DialogResult.Yes)
                    return;

                using (AppDbContext dbContext = new AppDbContext())
                {
                    ProductService productService = new ProductService(dbContext);
                    productService.DeleteProduct(product.Id);
                }
            }

            RemoveProductFromGrid(product, 0);
        }

        private void DeleteSelectedProduct()
        {
            int rowIndex = GetSelectedProductRowIndex();
            if (rowIndex >= 0)
                DeleteProductAtRow(rowIndex);
        }

        private int GetSelectedProductRowIndex()
        {
            if (_productsGrid.CurrentRow == null)
                return -1;

            int rowIndex = _productsGrid.CurrentRow.Index;
            return rowIndex >= 0 && rowIndex < _products.Count ? rowIndex : -1;
        }

        private void RemoveProductFromGrid(Product product, int selectedProductId)
        {
            _products.Remove(product);
            RefreshProductComboBox(selectedProductId);
            RefreshProductsGrid();
        }

        private static Product CopyProduct(Product product)
        {
            return new Product
            {
                Id = product.Id,
                Name = product.Name,
                CustomerPartNumber = product.CustomerPartNumber,
                Shipcode = product.Shipcode,
                SerialStartValue = product.SerialStartValue,
                CodeGeneratorType = product.CodeGeneratorType,
                TemplatePath = product.TemplatePath,
                Pattern = product.Pattern
            };
        }

        private static string BuildProductLabel(Product product)
        {
            string part = string.IsNullOrWhiteSpace(product.CustomerPartNumber) ? "-" : product.CustomerPartNumber;
            return $"{product.Name} [{part}]";
        }

        private void SelectProduct(int productId)
        {
            if (productId <= 0)
            {
                _productComboBox.SelectedIndex = _productComboBox.Items.Count > 0 ? 0 : -1;
                return;
            }

            for (int i = 0; i < _productComboBox.Items.Count; i++)
            {
                Selection<Product> selection = (Selection<Product>)_productComboBox.Items[i];
                if (selection.Value.Id == productId)
                {
                    _productComboBox.SelectedIndex = i;
                    return;
                }
            }

            _productComboBox.SelectedIndex = _productComboBox.Items.Count > 0 ? 0 : -1;
        }

        private void ShowConfiguration(AppConfiguration configuration)
        {
            _machinePathTextBox.Text = configuration.MachinePath;
            _variableTextAliasTextBox.Text = configuration.VariableTextAlias;
            _useFootPedal.Checked = configuration.UseFootPedal;
            _dllVersionLabel.Text = Resources.not_read;
            _footPedalTimeoutSeconds.Value = Math.Max(
                _footPedalTimeoutSeconds.Minimum,
                Math.Min(_footPedalTimeoutSeconds.Maximum, configuration.FootPedalTimeoutMs / (decimal)1000));
            SelectProduct(configuration.ProductId);
        }

        private Product GetSelectedProduct()
        {
            return _productComboBox.SelectedItem is Selection<Product> selection ? selection.Value : null;
        }

        private void SaveProduct(Product product)
        {
            try
            {
                ValidateProduct(product);

                using (AppDbContext dbContext = new AppDbContext())
                {
                    ProductService productService = new ProductService(dbContext);
                    if (product.Id == 0)
                        productService.AddProduct(product);
                    else
                        productService.UpdateProduct(product);
                }

                LoadProducts(product.Id);
                SelectProduct(product.Id);
                MessageBox.Show(GetRootOwner(this), Resources.product_saved_message, Resources.prod_config, MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(GetRootOwner(this), string.Format(Resources.product_save_failed_fmt, ex.Message), Resources.prod_config, MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
            }
        }

        private static void ValidateProduct(Product product)
        {
            if (string.IsNullOrWhiteSpace(product.Name))
                throw new InvalidDataException(Resources.product_name_required);
            if (string.IsNullOrWhiteSpace(product.TemplatePath))
                throw new InvalidDataException(Resources.product_template_required);
            if (string.IsNullOrWhiteSpace(product.Pattern))
                throw new InvalidDataException(Resources.pattern_required);
            if (product.SerialStartValue < 1 || product.SerialStartValue > 9999)
                throw new InvalidDataException(Resources.serial_start_range);
            if (!IsCodeGeneratorTypeValid(product.CodeGeneratorType))
                throw new InvalidDataException(Resources.generator_invalid);
        }

        private void ReadDllVersion()
        {
            string machinePath = _machinePathTextBox.Text.Trim();
            if (string.IsNullOrWhiteSpace(machinePath))
            {
                _dllVersionLabel.Text = Resources.fill_device_config_dir;
                return;
            }

            string dllPath = Path.Combine(machinePath, "HansAdvInterface.dll");
            try
            {
                if (!File.Exists(dllPath))
                {
                    _dllVersionLabel.Text = Resources.read_failed_dll_missing;
                    return;
                }

                using (HansApi api = new HansApi(dllPath))
                {
                    _dllVersionLabel.Text = api.GetVersionText();
                }
            }
            catch (Exception ex)
            {
                _dllVersionLabel.Text = string.Format(Resources.read_failed_fmt, ex.Message);
            }
        }

        private void BrowseFile(TextBox target, string title, string filter)
        {
            using (OpenFileDialog dialog = new OpenFileDialog())
            {
                dialog.Title = title;
                dialog.Filter = filter;
                dialog.CheckFileExists = true;
                dialog.CheckPathExists = true;

                string currentPath = target.Text.Trim();
                if (File.Exists(currentPath))
                {
                    dialog.FileName = currentPath;
                    dialog.InitialDirectory = Path.GetDirectoryName(Path.GetFullPath(currentPath));
                }
                else if (Directory.Exists(currentPath))
                {
                    dialog.InitialDirectory = currentPath;
                }

                if (dialog.ShowDialog(this) == DialogResult.OK)
                    target.Text = dialog.FileName;
            }
        }

        private void BrowseFolder(TextBox target, string description)
        {
            using (FolderBrowserDialog dialog = new FolderBrowserDialog())
            {
                dialog.Description = description;
                dialog.ShowNewFolderButton = false;

                string currentPath = target.Text.Trim();
                if (Directory.Exists(currentPath))
                    dialog.SelectedPath = currentPath;
                else if (File.Exists(currentPath))
                    dialog.SelectedPath = Path.GetDirectoryName(Path.GetFullPath(currentPath));

                if (dialog.ShowDialog(this) == DialogResult.OK)
                    target.Text = dialog.SelectedPath;
            }
        }

        private static bool IsCodeGeneratorTypeValid(string generatorType)
        {
            return string.Equals(generatorType, CodeGeneratorTypes.Normal, StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(generatorType, CodeGeneratorTypes.EcoFlow, StringComparison.OrdinalIgnoreCase);
        }

        private void SaveAndClose()
        {
            if (_initialPage != SettingsPage.RunSettings)
                return;

            try
            {
                Product product = GetSelectedProduct();
                if (product == null)
                    return;

                AppConfiguration configuration = new AppConfiguration
                {
                    MachinePath = _machinePathTextBox.Text.Trim(),
                    TemplatePath = product.TemplatePath,
                    VariableTextAlias = _variableTextAliasTextBox.Text.Trim(),
                    UseFootPedal = _useFootPedal.Checked,
                    FootPedalTimeoutMs = Convert.ToInt32(_footPedalTimeoutSeconds.Value) * 1000,
                    ProductId = product.Id
                };

                if (Configuration == configuration)
                {
                    DialogResult = DialogResult.Cancel;
                    return;
                }

                Configuration = AppConfiguration.LoadFromJson(configuration.ToJson(), "config.json");
                DialogResult = DialogResult.OK;
            }
            catch (Exception ex)
            {
                MessageBox.Show(GetRootOwner(this), string.Format(Resources.settings_invalid_fmt, ex.Message), Resources.setting, MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
            }
        }

        private static IWin32Window GetRootOwner(Form form)
        {
            Form owner = form;
            while (owner != null && owner.Owner != null)
                owner = owner.Owner;

            return owner ?? form;
        }

        private sealed class ProductEditorDialog : Form
        {
            private readonly TextBox _nameTextBox;
            private readonly TextBox _customerPartNumberTextBox;
            private readonly NumericUpDown _shipcodeBox;
            private readonly NumericUpDown _serialStartValueBox;
            private readonly ComboBox _codeGeneratorComboBox;
            private readonly TextBox _templatePathTextBox;
            private readonly TextBox _patternTextBox;

            public Product Product { get; private set; }

            public ProductEditorDialog(Product product)
            {
                Product = product ?? throw new ArgumentNullException(nameof(product));

                Text = Product.Id == 0 ? Resources.new_product : Resources.edit_product;
                StartPosition = FormStartPosition.CenterParent;
                MinimumSize = new Size(620, 420);
                Size = new Size(700, 460);
                Font = new Font("Microsoft YaHei UI", 9F);

                TableLayoutPanel shell = new TableLayoutPanel
                {
                    Dock = DockStyle.Fill,
                    ColumnCount = 1,
                    RowCount = 2,
                    Padding = new Padding(14)
                };
                shell.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
                shell.RowStyles.Add(new RowStyle(SizeType.Absolute, 52));
                Controls.Add(shell);

                TableLayoutPanel grid = CreateDialogFormGrid(7);
                grid.ColumnStyles[0].Width = 110;
                shell.Controls.Add(grid, 0, 0);

                _nameTextBox = AddDialogTextBox(grid, 0, Resources.name);
                _customerPartNumberTextBox = AddDialogTextBox(grid, 1, Resources.customer_part_number);
                _shipcodeBox = AddDialogNumeric(grid, 2, "Shipcode", 0, 999999);
                _serialStartValueBox = AddDialogNumeric(grid, 3, Resources.serial_start, 1, 9999);
                _codeGeneratorComboBox = AddDialogComboBox(grid, 4, Resources.generator);
                _codeGeneratorComboBox.Items.Add(CodeGeneratorTypes.EcoFlow);
                _codeGeneratorComboBox.Items.Add(CodeGeneratorTypes.Normal);
                _templatePathTextBox = AddDialogPathTextBox(grid, 5, Resources.marking_template,
                    delegate(TextBox textBox) { BrowseFile(textBox, Resources.select_marking_template, "HS (*.HS)|*.HS|All files (*.*)|*.*"); });
                _patternTextBox = AddDialogTextBox(grid, 6, "Pattern");

                FlowLayoutPanel buttons = new FlowLayoutPanel
                {
                    Dock = DockStyle.Fill,
                    FlowDirection = FlowDirection.RightToLeft,
                    WrapContents = false,
                    Padding = new Padding(0, 10, 0, 0)
                };
                shell.Controls.Add(buttons, 0, 1);

                Button saveButton = new Button { Width = 90, Height = 32, Text = Resources.save };
                saveButton.Click += delegate { SaveAndClose(); };
                buttons.Controls.Add(saveButton);

                Button cancelButton = new Button { Width = 90, Height = 32, Text = Resources.cancel };
                cancelButton.Click += delegate { DialogResult = DialogResult.Cancel; };
                buttons.Controls.Add(cancelButton);

                AcceptButton = saveButton;
                CancelButton = cancelButton;

                ShowProduct();
            }

            private void ShowProduct()
            {
                _nameTextBox.Text = Product.Name;
                _customerPartNumberTextBox.Text = Product.CustomerPartNumber;
                _shipcodeBox.Value = Math.Max(_shipcodeBox.Minimum, Math.Min(_shipcodeBox.Maximum, Product.Shipcode));
                _serialStartValueBox.Value = Math.Max(_serialStartValueBox.Minimum,
                    Math.Min(_serialStartValueBox.Maximum,
                        Product.SerialStartValue <= 0 ? 1 : Product.SerialStartValue));
                SelectCodeGenerator(Product.CodeGeneratorType);
                _templatePathTextBox.Text = Product.TemplatePath;
                _patternTextBox.Text = Product.Pattern;
            }

            private void SaveAndClose()
            {
                try
                {
                    Product.Name = _nameTextBox.Text.Trim();
                    Product.CustomerPartNumber = _customerPartNumberTextBox.Text.Trim();
                    Product.Shipcode = Convert.ToInt32(_shipcodeBox.Value);
                    Product.SerialStartValue = Convert.ToInt32(_serialStartValueBox.Value);
                    Product.CodeGeneratorType = _codeGeneratorComboBox.SelectedItem == null
                        ? CodeGeneratorTypes.EcoFlow
                        : _codeGeneratorComboBox.SelectedItem.ToString();
                    Product.TemplatePath = _templatePathTextBox.Text.Trim();
                    Product.Pattern = _patternTextBox.Text.Trim();

                    ValidateProduct(Product);
                    DialogResult = DialogResult.OK;
                }
                catch (Exception ex)
                {
                    MessageBox.Show(GetRootOwner(this), string.Format(Resources.product_settings_invalid_fmt, ex.Message), Resources.prod_config,
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }

            private void SelectCodeGenerator(string generatorType)
            {
                string value = string.IsNullOrWhiteSpace(generatorType)
                    ? CodeGeneratorTypes.EcoFlow
                    : generatorType;

                for (int i = 0; i < _codeGeneratorComboBox.Items.Count; i++)
                {
                    if (string.Equals(_codeGeneratorComboBox.Items[i].ToString(), value,
                            StringComparison.OrdinalIgnoreCase))
                    {
                        _codeGeneratorComboBox.SelectedIndex = i;
                        return;
                    }
                }

                _codeGeneratorComboBox.SelectedIndex = 0;
            }

            private static TableLayoutPanel CreateDialogFormGrid(int rows)
            {
                TableLayoutPanel grid = new TableLayoutPanel
                {
                    Dock = DockStyle.Fill,
                    ColumnCount = 2,
                    RowCount = rows,
                    Padding = new Padding(12)
                };
                grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150));
                grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
                for (int i = 0; i < rows; i++)
                    grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
                return grid;
            }

            private static void AddDialogLabel(TableLayoutPanel grid, int row, string label)
            {
                Label name = new Label
                {
                    Dock = DockStyle.Fill,
                    Text = label,
                    TextAlign = ContentAlignment.MiddleLeft
                };
                grid.Controls.Add(name, 0, row);
            }

            private static TextBox AddDialogTextBox(TableLayoutPanel grid, int row, string label)
            {
                AddDialogLabel(grid, row, label);
                TextBox textBox = new TextBox
                {
                    Dock = DockStyle.Fill,
                    Margin = new Padding(0, 8, 0, 0)
                };
                grid.Controls.Add(textBox, 1, row);
                return textBox;
            }

            private static NumericUpDown AddDialogNumeric(TableLayoutPanel grid, int row, string label,
                decimal minimum, decimal maximum)
            {
                AddDialogLabel(grid, row, label);
                NumericUpDown numeric = new NumericUpDown
                {
                    Dock = DockStyle.Left,
                    Minimum = minimum,
                    Maximum = maximum,
                    Width = 140,
                    Margin = new Padding(0, 8, 0, 0)
                };
                grid.Controls.Add(numeric, 1, row);
                return numeric;
            }

            private static ComboBox AddDialogComboBox(TableLayoutPanel grid, int row, string label)
            {
                AddDialogLabel(grid, row, label);
                ComboBox comboBox = new ComboBox
                {
                    Dock = DockStyle.Fill,
                    DropDownStyle = ComboBoxStyle.DropDownList,
                    Margin = new Padding(0, 8, 0, 0)
                };
                grid.Controls.Add(comboBox, 1, row);
                return comboBox;
            }

            private TextBox AddDialogPathTextBox(TableLayoutPanel grid, int row, string label,
                Action<TextBox> browseAction)
            {
                AddDialogLabel(grid, row, label);

                TableLayoutPanel panel = new TableLayoutPanel
                {
                    Dock = DockStyle.Fill,
                    ColumnCount = 2,
                    Margin = Padding.Empty
                };
                panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
                panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 78));
                grid.Controls.Add(panel, 1, row);

                TextBox textBox = new TextBox
                {
                    Dock = DockStyle.Fill,
                    Margin = new Padding(0, 8, 8, 0)
                };
                panel.Controls.Add(textBox, 0, 0);

                Button browseButton = new Button
                {
                    Dock = DockStyle.Fill,
                    Margin = new Padding(0, 6, 0, 4),
                    Text = Resources.open
                };
                browseButton.Click += delegate { browseAction(textBox); };
                panel.Controls.Add(browseButton, 1, 0);
                return textBox;
            }

            private void BrowseFile(TextBox target, string title, string filter)
            {
                using (OpenFileDialog dialog = new OpenFileDialog())
                {
                    dialog.Title = title;
                    dialog.Filter = filter;
                    dialog.CheckFileExists = true;
                    dialog.CheckPathExists = true;

                    string currentPath = target.Text.Trim();
                    if (File.Exists(currentPath))
                    {
                        dialog.FileName = currentPath;
                        dialog.InitialDirectory = Path.GetDirectoryName(Path.GetFullPath(currentPath));
                    }
                    else if (Directory.Exists(currentPath))
                    {
                        dialog.InitialDirectory = currentPath;
                    }

                    if (dialog.ShowDialog(this) == DialogResult.OK)
                        target.Text = dialog.FileName;
                }
            }
        }
    }
}
