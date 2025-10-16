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
            topPanel = new Panel();
            reloadConfigButton = new Button();
            deviceFilterComboBox = new ComboBox();
            filterLabel = new Label();
            connectionStatusLabel = new Label();
            statusListView = new ListView();
            deviceColumnHeader = new ColumnHeader();
            statusColumnHeader = new ColumnHeader();
            updatedColumnHeader = new ColumnHeader();
            messagesGrid = new DataGridView();
            topPanel.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)messagesGrid).BeginInit();
            SuspendLayout();
            // 
            // topPanel
            // 
            topPanel.Controls.Add(reloadConfigButton);
            topPanel.Controls.Add(deviceFilterComboBox);
            topPanel.Controls.Add(filterLabel);
            topPanel.Controls.Add(connectionStatusLabel);
            topPanel.Dock = DockStyle.Top;
            topPanel.Location = new Point(0, 0);
            topPanel.Name = "topPanel";
            topPanel.Size = new Size(984, 48);
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
            filterLabel.Text = "Device filter";
            // 
            // connectionStatusLabel
            // 
            connectionStatusLabel.AutoSize = true;
            connectionStatusLabel.Font = new Font("Segoe UI", 9F, FontStyle.Bold, GraphicsUnit.Point);
            connectionStatusLabel.Location = new Point(12, 15);
            connectionStatusLabel.Name = "connectionStatusLabel";
            connectionStatusLabel.Size = new Size(174, 20);
            connectionStatusLabel.TabIndex = 0;
            connectionStatusLabel.Text = "Connected: 0 / 0 devices";
            // 
            // statusListView
            // 
            statusListView.Columns.AddRange(new ColumnHeader[] { deviceColumnHeader, statusColumnHeader, updatedColumnHeader });
            statusListView.Dock = DockStyle.Top;
            statusListView.FullRowSelect = true;
            statusListView.GridLines = true;
            statusListView.Location = new Point(0, 48);
            statusListView.MultiSelect = false;
            statusListView.Name = "statusListView";
            statusListView.Size = new Size(984, 146);
            statusListView.TabIndex = 1;
            statusListView.UseCompatibleStateImageBehavior = false;
            statusListView.View = View.Details;
            // 
            // deviceColumnHeader
            // 
            deviceColumnHeader.Text = "Device";
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
            // messagesGrid
            // 
            messagesGrid.AllowUserToAddRows = false;
            messagesGrid.AllowUserToDeleteRows = false;
            messagesGrid.AllowUserToResizeRows = false;
            messagesGrid.BackgroundColor = SystemColors.Window;
            messagesGrid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            messagesGrid.Dock = DockStyle.Fill;
            messagesGrid.Location = new Point(0, 194);
            messagesGrid.MultiSelect = false;
            messagesGrid.Name = "messagesGrid";
            messagesGrid.ReadOnly = true;
            messagesGrid.RowHeadersVisible = false;
            messagesGrid.RowTemplate.Height = 29;
            messagesGrid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            messagesGrid.Size = new Size(984, 467);
            messagesGrid.TabIndex = 2;
            // 
            // MainForm
            // 
            AutoScaleDimensions = new SizeF(8F, 20F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(984, 661);
            Controls.Add(messagesGrid);
            Controls.Add(statusListView);
            Controls.Add(topPanel);
            MinimumSize = new Size(800, 500);
            Name = "MainForm";
            StartPosition = FormStartPosition.CenterScreen;
            Text = "GeoTrack";
            Load += MainForm_Load;
            topPanel.ResumeLayout(false);
            topPanel.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)messagesGrid).EndInit();
            ResumeLayout(false);
        }

        #endregion

        private Panel topPanel;
        private Button reloadConfigButton;
        private ComboBox deviceFilterComboBox;
        private Label filterLabel;
        private Label connectionStatusLabel;
        private ListView statusListView;
        private ColumnHeader deviceColumnHeader;
        private ColumnHeader statusColumnHeader;
        private ColumnHeader updatedColumnHeader;
        private DataGridView messagesGrid;
    }
}
