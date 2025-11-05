using System.Drawing;
using System.Windows.Forms;

namespace GeoTrack
{
    partial class MainForm
    {
        /// <summary>
        ///  Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        ///  Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        ///  Required method for Designer support - do not modify
        ///  the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            components = new System.ComponentModel.Container();
            topPanel = new Panel();
            reloadConfigButton = new Button();
            deviceFilterComboBox = new ComboBox();
            filterLabel = new Label();
            connectionStatusLabel = new Label();
            externalStatusLabel = new Label();
            statusListView = new ListView();
            deviceColumnHeader = new ColumnHeader();
            statusColumnHeader = new ColumnHeader();
            updatedColumnHeader = new ColumnHeader();
            mainTabControl = new TabControl();
            filteredTabPage = new TabPage();
            filteredGrid = new DataGridView();
            telemetryTabPage = new TabPage();
            telemetryGrid = new DataGridView();
            logPanel = new Panel();
            logTextBox = new TextBox();
            logLabel = new Label();
            trayContextMenu = new ContextMenuStrip(components);
            openMenuItem = new ToolStripMenuItem();
            toggleSendingMenuItem = new ToolStripMenuItem();
            exitMenuItem = new ToolStripMenuItem();
            trayNotifyIcon = new NotifyIcon(components);
            topPanel.SuspendLayout();
            filteredTabPage.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)filteredGrid).BeginInit();
            telemetryTabPage.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)telemetryGrid).BeginInit();
            logPanel.SuspendLayout();
            trayContextMenu.SuspendLayout();
            SuspendLayout();
            //
            // topPanel
            //
            topPanel.Controls.Add(externalStatusLabel);
            topPanel.Controls.Add(reloadConfigButton);
            topPanel.Controls.Add(deviceFilterComboBox);
            topPanel.Controls.Add(filterLabel);
            topPanel.Controls.Add(connectionStatusLabel);
            topPanel.Dock = DockStyle.Top;
            topPanel.Location = new Point(0, 0);
            topPanel.Name = "topPanel";
            topPanel.Size = new Size(984, 64);
            topPanel.TabIndex = 0;
            //
            // reloadConfigButton
            //
            reloadConfigButton.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            reloadConfigButton.Location = new Point(863, 10);
            reloadConfigButton.Name = "reloadConfigButton";
            reloadConfigButton.Size = new Size(109, 29);
            reloadConfigButton.TabIndex = 3;
            reloadConfigButton.Text = "Reload Config";
            reloadConfigButton.UseVisualStyleBackColor = true;
            reloadConfigButton.Click += ReloadConfigButton_Click;
            //
            // deviceFilterComboBox
            //
            deviceFilterComboBox.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            deviceFilterComboBox.DropDownStyle = ComboBoxStyle.DropDownList;
            deviceFilterComboBox.FormattingEnabled = true;
            deviceFilterComboBox.Location = new Point(655, 11);
            deviceFilterComboBox.Name = "deviceFilterComboBox";
            deviceFilterComboBox.Size = new Size(202, 28);
            deviceFilterComboBox.TabIndex = 2;
            deviceFilterComboBox.SelectedIndexChanged += DeviceFilterComboBox_SelectedIndexChanged;
            //
            // filterLabel
            //
            filterLabel.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            filterLabel.AutoSize = true;
            filterLabel.Location = new Point(570, 15);
            filterLabel.Name = "filterLabel";
            filterLabel.Size = new Size(79, 20);
            filterLabel.TabIndex = 1;
            filterLabel.Text = "Station filter";
            //
            // connectionStatusLabel
            //
            connectionStatusLabel.AutoSize = true;
            connectionStatusLabel.Font = new Font("Segoe UI", 9F, FontStyle.Bold, GraphicsUnit.Point);
            connectionStatusLabel.Location = new Point(12, 12);
            connectionStatusLabel.Name = "connectionStatusLabel";
            connectionStatusLabel.Size = new Size(174, 20);
            connectionStatusLabel.TabIndex = 0;
            connectionStatusLabel.Text = "Connected: 0 / 0 devices";
            //
            // externalStatusLabel
            //
            externalStatusLabel.AutoSize = true;
            externalStatusLabel.Location = new Point(12, 36);
            externalStatusLabel.Name = "externalStatusLabel";
            externalStatusLabel.Size = new Size(176, 20);
            externalStatusLabel.TabIndex = 4;
            externalStatusLabel.Text = "External app: Disabled";
            //
            // statusListView
            //
            statusListView.Columns.AddRange(new ColumnHeader[] { deviceColumnHeader, statusColumnHeader, updatedColumnHeader });
            statusListView.Dock = DockStyle.Top;
            statusListView.FullRowSelect = true;
            statusListView.GridLines = true;
            statusListView.Location = new Point(0, 64);
            statusListView.MultiSelect = false;
            statusListView.Name = "statusListView";
            statusListView.Size = new Size(984, 146);
            statusListView.TabIndex = 1;
            statusListView.UseCompatibleStateImageBehavior = false;
            statusListView.View = View.Details;
            //
            // deviceColumnHeader
            //
            deviceColumnHeader.Text = "Station";
            deviceColumnHeader.Width = 200;
            //
            // statusColumnHeader
            //
            statusColumnHeader.Text = "Status";
            statusColumnHeader.Width = 220;
            //
            // updatedColumnHeader
            //
            updatedColumnHeader.Text = "Updated";
            updatedColumnHeader.Width = 200;
            //
            // mainTabControl
            //
            mainTabControl.Controls.Add(filteredTabPage);
            mainTabControl.Controls.Add(telemetryTabPage);
            mainTabControl.Dock = DockStyle.Fill;
            mainTabControl.Location = new Point(0, 210);
            mainTabControl.Name = "mainTabControl";
            mainTabControl.SelectedIndex = 0;
            mainTabControl.Size = new Size(984, 301);
            mainTabControl.TabIndex = 2;
            //
            // filteredTabPage
            //
            filteredTabPage.Controls.Add(filteredGrid);
            filteredTabPage.Location = new Point(4, 29);
            filteredTabPage.Name = "filteredTabPage";
            filteredTabPage.Padding = new Padding(3);
            filteredTabPage.Size = new Size(976, 268);
            filteredTabPage.TabIndex = 0;
            filteredTabPage.Text = "Filtered Buggies";
            filteredTabPage.UseVisualStyleBackColor = true;
            //
            // filteredGrid
            //
            filteredGrid.AllowUserToAddRows = false;
            filteredGrid.AllowUserToDeleteRows = false;
            filteredGrid.AllowUserToResizeRows = false;
            filteredGrid.BackgroundColor = SystemColors.Window;
            filteredGrid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            filteredGrid.Dock = DockStyle.Fill;
            filteredGrid.Location = new Point(3, 3);
            filteredGrid.MultiSelect = false;
            filteredGrid.Name = "filteredGrid";
            filteredGrid.ReadOnly = true;
            filteredGrid.RowHeadersVisible = false;
            filteredGrid.RowTemplate.Height = 29;
            filteredGrid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            filteredGrid.Size = new Size(970, 262);
            filteredGrid.TabIndex = 0;
            //
            // telemetryTabPage
            //
            telemetryTabPage.Controls.Add(telemetryGrid);
            telemetryTabPage.Location = new Point(4, 29);
            telemetryTabPage.Name = "telemetryTabPage";
            telemetryTabPage.Padding = new Padding(3);
            telemetryTabPage.Size = new Size(976, 268);
            telemetryTabPage.TabIndex = 1;
            telemetryTabPage.Text = "Telemetry";
            telemetryTabPage.UseVisualStyleBackColor = true;
            //
            // telemetryGrid
            //
            telemetryGrid.AllowUserToAddRows = false;
            telemetryGrid.AllowUserToDeleteRows = false;
            telemetryGrid.AllowUserToResizeRows = false;
            telemetryGrid.BackgroundColor = SystemColors.Window;
            telemetryGrid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            telemetryGrid.Dock = DockStyle.Fill;
            telemetryGrid.Location = new Point(3, 3);
            telemetryGrid.MultiSelect = false;
            telemetryGrid.Name = "telemetryGrid";
            telemetryGrid.ReadOnly = true;
            telemetryGrid.RowHeadersVisible = false;
            telemetryGrid.RowTemplate.Height = 29;
            telemetryGrid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            telemetryGrid.Size = new Size(970, 262);
            telemetryGrid.TabIndex = 0;
            //
            // logPanel
            //
            logPanel.Controls.Add(logTextBox);
            logPanel.Controls.Add(logLabel);
            logPanel.Dock = DockStyle.Bottom;
            logPanel.Location = new Point(0, 511);
            logPanel.Name = "logPanel";
            logPanel.Size = new Size(984, 150);
            logPanel.TabIndex = 3;
            //
            // logTextBox
            //
            logTextBox.Dock = DockStyle.Fill;
            logTextBox.Location = new Point(0, 23);
            logTextBox.Multiline = true;
            logTextBox.Name = "logTextBox";
            logTextBox.ReadOnly = true;
            logTextBox.ScrollBars = ScrollBars.Vertical;
            logTextBox.Size = new Size(984, 127);
            logTextBox.TabIndex = 1;
            //
            // logLabel
            //
            logLabel.Dock = DockStyle.Top;
            logLabel.Location = new Point(0, 0);
            logLabel.Name = "logLabel";
            logLabel.Size = new Size(984, 23);
            logLabel.TabIndex = 0;
            logLabel.Text = "Logs";
            logLabel.TextAlign = ContentAlignment.MiddleLeft;
            //
            // trayContextMenu
            //
            trayContextMenu.ImageScalingSize = new Size(20, 20);
            trayContextMenu.Items.AddRange(new ToolStripItem[] { openMenuItem, toggleSendingMenuItem, exitMenuItem });
            trayContextMenu.Name = "trayContextMenu";
            trayContextMenu.Size = new Size(197, 76);
            //
            // openMenuItem
            //
            openMenuItem.Name = "openMenuItem";
            openMenuItem.Size = new Size(196, 24);
            openMenuItem.Text = "Open";
            openMenuItem.Click += OpenMenuItem_Click;
            //
            // toggleSendingMenuItem
            //
            toggleSendingMenuItem.Name = "toggleSendingMenuItem";
            toggleSendingMenuItem.Size = new Size(196, 24);
            toggleSendingMenuItem.Text = "Pause Sending";
            toggleSendingMenuItem.Click += ToggleSendingMenuItem_Click;
            //
            // exitMenuItem
            //
            exitMenuItem.Name = "exitMenuItem";
            exitMenuItem.Size = new Size(196, 24);
            exitMenuItem.Text = "Exit";
            exitMenuItem.Click += ExitMenuItem_Click;
            //
            // trayNotifyIcon
            //
            trayNotifyIcon.BalloonTipIcon = ToolTipIcon.Info;
            trayNotifyIcon.ContextMenuStrip = trayContextMenu;
            trayNotifyIcon.Text = "GeoTrack";
            trayNotifyIcon.Visible = false;
            trayNotifyIcon.DoubleClick += TrayNotifyIcon_DoubleClick;
            //
            // MainForm
            //
            AutoScaleDimensions = new SizeF(8F, 20F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(984, 661);
            Controls.Add(mainTabControl);
            Controls.Add(logPanel);
            Controls.Add(statusListView);
            Controls.Add(topPanel);
            MinimumSize = new Size(800, 500);
            Name = "MainForm";
            StartPosition = FormStartPosition.CenterScreen;
            Text = "GeoTrack";
            Load += MainForm_Load;
            FormClosing += MainForm_FormClosing;
            Resize += MainForm_Resize;
            topPanel.ResumeLayout(false);
            topPanel.PerformLayout();
            filteredTabPage.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)filteredGrid).EndInit();
            telemetryTabPage.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)telemetryGrid).EndInit();
            logPanel.ResumeLayout(false);
            logPanel.PerformLayout();
            trayContextMenu.ResumeLayout(false);
            ResumeLayout(false);
        }

        #endregion

        private Panel topPanel;
        private Button reloadConfigButton;
        private ComboBox deviceFilterComboBox;
        private Label filterLabel;
        private Label connectionStatusLabel;
        private Label externalStatusLabel;
        private ListView statusListView;
        private ColumnHeader deviceColumnHeader;
        private ColumnHeader statusColumnHeader;
        private ColumnHeader updatedColumnHeader;
        private TabControl mainTabControl;
        private TabPage filteredTabPage;
        private DataGridView filteredGrid;
        private TabPage telemetryTabPage;
        private DataGridView telemetryGrid;
        private Panel logPanel;
        private TextBox logTextBox;
        private Label logLabel;
        private ContextMenuStrip trayContextMenu;
        private ToolStripMenuItem openMenuItem;
        private ToolStripMenuItem toggleSendingMenuItem;
        private ToolStripMenuItem exitMenuItem;
        private NotifyIcon trayNotifyIcon;
    }
}
