namespace Gw2Patcher.UI
{
    partial class formWarnings
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
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
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle3 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle1 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle2 = new System.Windows.Forms.DataGridViewCellStyle();
            this.gridRequests = new System.Windows.Forms.DataGridView();
            this.columnUrl = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.columnStatus = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.columnDetails = new System.Windows.Forms.DataGridViewLinkColumn();
            ((System.ComponentModel.ISupportInitialize)(this.gridRequests)).BeginInit();
            this.SuspendLayout();
            // 
            // gridRequests
            // 
            this.gridRequests.AllowUserToAddRows = false;
            this.gridRequests.AllowUserToDeleteRows = false;
            this.gridRequests.AllowUserToResizeColumns = false;
            this.gridRequests.AllowUserToResizeRows = false;
            this.gridRequests.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.gridRequests.BackgroundColor = System.Drawing.SystemColors.Control;
            this.gridRequests.BorderStyle = System.Windows.Forms.BorderStyle.None;
            this.gridRequests.CellBorderStyle = System.Windows.Forms.DataGridViewCellBorderStyle.None;
            this.gridRequests.ColumnHeadersBorderStyle = System.Windows.Forms.DataGridViewHeaderBorderStyle.None;
            this.gridRequests.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.gridRequests.Columns.AddRange(new System.Windows.Forms.DataGridViewColumn[] {
            this.columnUrl,
            this.columnStatus,
            this.columnDetails});
            dataGridViewCellStyle3.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleLeft;
            dataGridViewCellStyle3.BackColor = System.Drawing.SystemColors.Control;
            dataGridViewCellStyle3.Font = new System.Drawing.Font("Segoe UI", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            dataGridViewCellStyle3.ForeColor = System.Drawing.SystemColors.ControlText;
            dataGridViewCellStyle3.SelectionBackColor = System.Drawing.SystemColors.ControlLight;
            dataGridViewCellStyle3.SelectionForeColor = System.Drawing.SystemColors.ControlText;
            dataGridViewCellStyle3.WrapMode = System.Windows.Forms.DataGridViewTriState.False;
            this.gridRequests.DefaultCellStyle = dataGridViewCellStyle3;
            this.gridRequests.EditMode = System.Windows.Forms.DataGridViewEditMode.EditProgrammatically;
            this.gridRequests.Location = new System.Drawing.Point(12, 12);
            this.gridRequests.MultiSelect = false;
            this.gridRequests.Name = "gridRequests";
            this.gridRequests.ReadOnly = true;
            this.gridRequests.RowHeadersVisible = false;
            this.gridRequests.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
            this.gridRequests.SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.FullRowSelect;
            this.gridRequests.Size = new System.Drawing.Size(463, 197);
            this.gridRequests.TabIndex = 50;
            this.gridRequests.CellContentClick += new System.Windows.Forms.DataGridViewCellEventHandler(this.gridRequests_CellContentClick);
            this.gridRequests.SelectionChanged += new System.EventHandler(this.gridRequests_SelectionChanged);
            // 
            // columnUrl
            // 
            this.columnUrl.AutoSizeMode = System.Windows.Forms.DataGridViewAutoSizeColumnMode.Fill;
            this.columnUrl.HeaderText = "Request";
            this.columnUrl.Name = "columnUrl";
            this.columnUrl.ReadOnly = true;
            this.columnUrl.SortMode = System.Windows.Forms.DataGridViewColumnSortMode.NotSortable;
            // 
            // columnStatus
            // 
            dataGridViewCellStyle1.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleRight;
            this.columnStatus.DefaultCellStyle = dataGridViewCellStyle1;
            this.columnStatus.HeaderText = "";
            this.columnStatus.Name = "columnStatus";
            this.columnStatus.ReadOnly = true;
            this.columnStatus.SortMode = System.Windows.Forms.DataGridViewColumnSortMode.NotSortable;
            // 
            // columnDetails
            // 
            this.columnDetails.ActiveLinkColor = System.Drawing.Color.FromArgb(((int)(((byte)(49)))), ((int)(((byte)(121)))), ((int)(((byte)(242)))));
            dataGridViewCellStyle2.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleCenter;
            this.columnDetails.DefaultCellStyle = dataGridViewCellStyle2;
            this.columnDetails.HeaderText = "";
            this.columnDetails.LinkBehavior = System.Windows.Forms.LinkBehavior.HoverUnderline;
            this.columnDetails.LinkColor = System.Drawing.Color.FromArgb(((int)(((byte)(49)))), ((int)(((byte)(121)))), ((int)(((byte)(242)))));
            this.columnDetails.Name = "columnDetails";
            this.columnDetails.ReadOnly = true;
            this.columnDetails.TrackVisitedState = false;
            this.columnDetails.VisitedLinkColor = System.Drawing.Color.FromArgb(((int)(((byte)(49)))), ((int)(((byte)(121)))), ((int)(((byte)(242)))));
            this.columnDetails.Width = 70;
            // 
            // formWarnings
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(487, 221);
            this.Controls.Add(this.gridRequests);
            this.Font = new System.Drawing.Font("Segoe UI", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.MinimizeBox = false;
            this.Name = "formWarnings";
            this.Icon = global::Gw2Patcher.Properties.Resources.Gw2;
            this.ShowIcon = false;
            this.ShowInTaskbar = false;
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "Warnings";
            this.Load += new System.EventHandler(this.formWarnings_Load);
            ((System.ComponentModel.ISupportInitialize)(this.gridRequests)).EndInit();
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.DataGridView gridRequests;
        private System.Windows.Forms.DataGridViewTextBoxColumn columnUrl;
        private System.Windows.Forms.DataGridViewTextBoxColumn columnStatus;
        private System.Windows.Forms.DataGridViewLinkColumn columnDetails;
    }
}