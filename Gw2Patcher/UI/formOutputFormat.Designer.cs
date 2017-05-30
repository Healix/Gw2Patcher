namespace Gw2Patcher.UI
{
    partial class formOutputFormat
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
            this.textUpdateOutputFormat = new System.Windows.Forms.TextBox();
            this.label19 = new System.Windows.Forms.Label();
            this.labelVariableFilename = new System.Windows.Forms.Label();
            this.label21 = new System.Windows.Forms.Label();
            this.labelVariableUrl = new System.Windows.Forms.Label();
            this.label20 = new System.Windows.Forms.Label();
            this.label24 = new System.Windows.Forms.Label();
            this.buttonGenerateUpdateOutput = new System.Windows.Forms.Button();
            this.SuspendLayout();
            // 
            // textUpdateOutputFormat
            // 
            this.textUpdateOutputFormat.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.textUpdateOutputFormat.Location = new System.Drawing.Point(17, 45);
            this.textUpdateOutputFormat.Multiline = true;
            this.textUpdateOutputFormat.Name = "textUpdateOutputFormat";
            this.textUpdateOutputFormat.Size = new System.Drawing.Size(251, 60);
            this.textUpdateOutputFormat.TabIndex = 51;
            this.textUpdateOutputFormat.Text = "$filename $url";
            this.textUpdateOutputFormat.WordWrap = false;
            // 
            // label19
            // 
            this.label19.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.label19.AutoSize = true;
            this.label19.Font = new System.Drawing.Font("Segoe UI Semilight", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label19.Location = new System.Drawing.Point(14, 111);
            this.label19.Name = "label19";
            this.label19.Size = new System.Drawing.Size(49, 13);
            this.label19.TabIndex = 56;
            this.label19.Text = "Variables";
            // 
            // labelVariableFilename
            // 
            this.labelVariableFilename.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.labelVariableFilename.AutoSize = true;
            this.labelVariableFilename.Cursor = System.Windows.Forms.Cursors.Hand;
            this.labelVariableFilename.Font = new System.Drawing.Font("Segoe UI Semibold", 8.25F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.labelVariableFilename.Location = new System.Drawing.Point(69, 111);
            this.labelVariableFilename.Name = "labelVariableFilename";
            this.labelVariableFilename.Size = new System.Drawing.Size(57, 13);
            this.labelVariableFilename.TabIndex = 57;
            this.labelVariableFilename.Text = "$filename";
            this.labelVariableFilename.Click += new System.EventHandler(this.labelVariable_Click);
            // 
            // label21
            // 
            this.label21.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.label21.AutoSize = true;
            this.label21.Font = new System.Drawing.Font("Segoe UI Semilight", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label21.ForeColor = System.Drawing.Color.DarkGray;
            this.label21.Location = new System.Drawing.Point(127, 111);
            this.label21.Name = "label21";
            this.label21.Size = new System.Drawing.Size(10, 13);
            this.label21.TabIndex = 58;
            this.label21.Text = "|";
            // 
            // labelVariableUrl
            // 
            this.labelVariableUrl.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.labelVariableUrl.AutoSize = true;
            this.labelVariableUrl.Cursor = System.Windows.Forms.Cursors.Hand;
            this.labelVariableUrl.Font = new System.Drawing.Font("Segoe UI Semibold", 8.25F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.labelVariableUrl.Location = new System.Drawing.Point(137, 111);
            this.labelVariableUrl.Name = "labelVariableUrl";
            this.labelVariableUrl.Size = new System.Drawing.Size(26, 13);
            this.labelVariableUrl.TabIndex = 59;
            this.labelVariableUrl.Text = "$url";
            this.labelVariableUrl.Click += new System.EventHandler(this.labelVariable_Click);
            // 
            // label20
            // 
            this.label20.AutoSize = true;
            this.label20.Font = new System.Drawing.Font("Segoe UI Semibold", 9F, System.Drawing.FontStyle.Bold);
            this.label20.Location = new System.Drawing.Point(13, 10);
            this.label20.Name = "label20";
            this.label20.Size = new System.Drawing.Size(84, 15);
            this.label20.TabIndex = 60;
            this.label20.Text = "Output format";
            // 
            // label24
            // 
            this.label24.AutoSize = true;
            this.label24.Font = new System.Drawing.Font("Segoe UI Semilight", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label24.Location = new System.Drawing.Point(14, 26);
            this.label24.Name = "label24";
            this.label24.Size = new System.Drawing.Size(195, 13);
            this.label24.TabIndex = 61;
            this.label24.Text = "The output will be formatted as specified";
            // 
            // buttonGenerateUpdateOutput
            // 
            this.buttonGenerateUpdateOutput.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.buttonGenerateUpdateOutput.Font = new System.Drawing.Font("Segoe UI", 8.25F);
            this.buttonGenerateUpdateOutput.Location = new System.Drawing.Point(277, 45);
            this.buttonGenerateUpdateOutput.Name = "buttonGenerateUpdateOutput";
            this.buttonGenerateUpdateOutput.Size = new System.Drawing.Size(74, 30);
            this.buttonGenerateUpdateOutput.TabIndex = 62;
            this.buttonGenerateUpdateOutput.Text = "OK";
            this.buttonGenerateUpdateOutput.UseVisualStyleBackColor = true;
            this.buttonGenerateUpdateOutput.Click += new System.EventHandler(this.buttonGenerateUpdateOutput_Click);
            // 
            // formOutputFormat
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(363, 133);
            this.Controls.Add(this.buttonGenerateUpdateOutput);
            this.Controls.Add(this.label24);
            this.Controls.Add(this.label20);
            this.Controls.Add(this.label19);
            this.Controls.Add(this.labelVariableFilename);
            this.Controls.Add(this.label21);
            this.Controls.Add(this.labelVariableUrl);
            this.Controls.Add(this.textUpdateOutputFormat);
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.MinimumSize = new System.Drawing.Size(329, 154);
            this.Name = "formOutputFormat";
            this.Icon = global::Gw2Patcher.Properties.Resources.Gw2;
            this.ShowIcon = false;
            this.ShowInTaskbar = false;
            this.StartPosition = System.Windows.Forms.FormStartPosition.Manual;
            this.Load += new System.EventHandler(this.formOutputFormat_Load);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.TextBox textUpdateOutputFormat;
        private System.Windows.Forms.Label label19;
        private System.Windows.Forms.Label labelVariableFilename;
        private System.Windows.Forms.Label label21;
        private System.Windows.Forms.Label labelVariableUrl;
        private System.Windows.Forms.Label label20;
        private System.Windows.Forms.Label label24;
        private System.Windows.Forms.Button buttonGenerateUpdateOutput;
    }
}