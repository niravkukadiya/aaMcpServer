// ────────────────────────────────────────────────────────────
//  Project     : aa Mcp Server
//  Author      : NK
//  Date        : 26-05-2026
// ────────────────────────────────────────────────────────────
// Copyright 2026 The aaMcpServer Authors
// SPDX-License-Identifier: Apache-2.0
namespace aaMcpServer.TestClient
{
    partial class MainForm
    {
        /// <summary>Required designer variable.</summary>
        private System.ComponentModel.IContainer components = null;

        private System.Windows.Forms.Panel pnlTop;
        private System.Windows.Forms.Label lblEndpoint;
        private System.Windows.Forms.TextBox txtEndpoint;
        private System.Windows.Forms.Button btnConnect;
        private System.Windows.Forms.Label lblStatus;

        private System.Windows.Forms.Panel pnlTool;
        private System.Windows.Forms.Label lblTool;
        private System.Windows.Forms.ComboBox cmbTools;
        private System.Windows.Forms.Button btnCall;
        private System.Windows.Forms.TextBox txtDescription;

        private System.Windows.Forms.SplitContainer splitMain;
        private System.Windows.Forms.Label lblArgs;
        private System.Windows.Forms.TextBox txtArgs;
        private System.Windows.Forms.Label lblResult;
        private System.Windows.Forms.TextBox txtResult;

        /// <summary>Clean up any resources being used.</summary>
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
            this.pnlTop = new System.Windows.Forms.Panel();
            this.lblStatus = new System.Windows.Forms.Label();
            this.btnConnect = new System.Windows.Forms.Button();
            this.txtEndpoint = new System.Windows.Forms.TextBox();
            this.lblEndpoint = new System.Windows.Forms.Label();
            this.pnlTool = new System.Windows.Forms.Panel();
            this.txtDescription = new System.Windows.Forms.TextBox();
            this.btnCall = new System.Windows.Forms.Button();
            this.cmbTools = new System.Windows.Forms.ComboBox();
            this.lblTool = new System.Windows.Forms.Label();
            this.splitMain = new System.Windows.Forms.SplitContainer();
            this.lblArgs = new System.Windows.Forms.Label();
            this.txtArgs = new System.Windows.Forms.TextBox();
            this.lblResult = new System.Windows.Forms.Label();
            this.txtResult = new System.Windows.Forms.TextBox();
            this.pnlTop.SuspendLayout();
            this.pnlTool.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.splitMain)).BeginInit();
            this.splitMain.Panel1.SuspendLayout();
            this.splitMain.Panel2.SuspendLayout();
            this.splitMain.SuspendLayout();
            this.SuspendLayout();
            //
            // pnlTop
            //
            this.pnlTop.Controls.Add(this.lblStatus);
            this.pnlTop.Controls.Add(this.btnConnect);
            this.pnlTop.Controls.Add(this.txtEndpoint);
            this.pnlTop.Controls.Add(this.lblEndpoint);
            this.pnlTop.Dock = System.Windows.Forms.DockStyle.Top;
            this.pnlTop.Location = new System.Drawing.Point(0, 0);
            this.pnlTop.Name = "pnlTop";
            this.pnlTop.Size = new System.Drawing.Size(884, 70);
            this.pnlTop.TabIndex = 0;
            //
            // lblStatus
            //
            this.lblStatus.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left)
            | System.Windows.Forms.AnchorStyles.Right)));
            this.lblStatus.ForeColor = System.Drawing.Color.DimGray;
            this.lblStatus.Location = new System.Drawing.Point(12, 44);
            this.lblStatus.Name = "lblStatus";
            this.lblStatus.Size = new System.Drawing.Size(860, 20);
            this.lblStatus.TabIndex = 3;
            this.lblStatus.Text = "Not connected. Start the server with --console, then click Connect.";
            //
            // btnConnect
            //
            this.btnConnect.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.btnConnect.Location = new System.Drawing.Point(710, 10);
            this.btnConnect.Name = "btnConnect";
            this.btnConnect.Size = new System.Drawing.Size(162, 26);
            this.btnConnect.TabIndex = 2;
            this.btnConnect.Text = "Connect / List Tools";
            this.btnConnect.UseVisualStyleBackColor = true;
            this.btnConnect.Click += new System.EventHandler(this.btnConnect_Click);
            //
            // txtEndpoint
            //
            this.txtEndpoint.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left)
            | System.Windows.Forms.AnchorStyles.Right)));
            this.txtEndpoint.Location = new System.Drawing.Point(76, 12);
            this.txtEndpoint.Name = "txtEndpoint";
            this.txtEndpoint.Size = new System.Drawing.Size(628, 20);
            this.txtEndpoint.TabIndex = 1;
            this.txtEndpoint.Text = "http://localhost:8080/mcp";
            //
            // lblEndpoint
            //
            this.lblEndpoint.AutoSize = true;
            this.lblEndpoint.Location = new System.Drawing.Point(12, 15);
            this.lblEndpoint.Name = "lblEndpoint";
            this.lblEndpoint.Size = new System.Drawing.Size(55, 13);
            this.lblEndpoint.TabIndex = 0;
            this.lblEndpoint.Text = "Endpoint:";
            //
            // pnlTool
            //
            this.pnlTool.Controls.Add(this.txtDescription);
            this.pnlTool.Controls.Add(this.btnCall);
            this.pnlTool.Controls.Add(this.cmbTools);
            this.pnlTool.Controls.Add(this.lblTool);
            this.pnlTool.Dock = System.Windows.Forms.DockStyle.Top;
            this.pnlTool.Location = new System.Drawing.Point(0, 70);
            this.pnlTool.Name = "pnlTool";
            this.pnlTool.Size = new System.Drawing.Size(884, 78);
            this.pnlTool.TabIndex = 1;
            //
            // txtDescription
            //
            this.txtDescription.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left)
            | System.Windows.Forms.AnchorStyles.Right)));
            this.txtDescription.BackColor = System.Drawing.SystemColors.Control;
            this.txtDescription.BorderStyle = System.Windows.Forms.BorderStyle.None;
            this.txtDescription.ForeColor = System.Drawing.Color.DimGray;
            this.txtDescription.Location = new System.Drawing.Point(12, 44);
            this.txtDescription.Multiline = true;
            this.txtDescription.Name = "txtDescription";
            this.txtDescription.ReadOnly = true;
            this.txtDescription.Size = new System.Drawing.Size(860, 28);
            this.txtDescription.TabIndex = 3;
            //
            // btnCall
            //
            this.btnCall.Enabled = false;
            this.btnCall.Location = new System.Drawing.Point(368, 9);
            this.btnCall.Name = "btnCall";
            this.btnCall.Size = new System.Drawing.Size(110, 26);
            this.btnCall.TabIndex = 2;
            this.btnCall.Text = "Call Tool";
            this.btnCall.UseVisualStyleBackColor = true;
            this.btnCall.Click += new System.EventHandler(this.btnCall_Click);
            //
            // cmbTools
            //
            this.cmbTools.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cmbTools.FormattingEnabled = true;
            this.cmbTools.Location = new System.Drawing.Point(56, 10);
            this.cmbTools.Name = "cmbTools";
            this.cmbTools.Size = new System.Drawing.Size(300, 21);
            this.cmbTools.TabIndex = 1;
            this.cmbTools.SelectedIndexChanged += new System.EventHandler(this.cmbTools_SelectedIndexChanged);
            //
            // lblTool
            //
            this.lblTool.AutoSize = true;
            this.lblTool.Location = new System.Drawing.Point(12, 13);
            this.lblTool.Name = "lblTool";
            this.lblTool.Size = new System.Drawing.Size(31, 13);
            this.lblTool.TabIndex = 0;
            this.lblTool.Text = "Tool:";
            //
            // splitMain
            //
            this.splitMain.Dock = System.Windows.Forms.DockStyle.Fill;
            this.splitMain.Location = new System.Drawing.Point(0, 148);
            this.splitMain.Name = "splitMain";
            this.splitMain.Orientation = System.Windows.Forms.Orientation.Horizontal;
            //
            // splitMain.Panel1
            //
            this.splitMain.Panel1.Controls.Add(this.txtArgs);
            this.splitMain.Panel1.Controls.Add(this.lblArgs);
            this.splitMain.Panel1MinSize = 80;
            //
            // splitMain.Panel2
            //
            this.splitMain.Panel2.Controls.Add(this.txtResult);
            this.splitMain.Panel2.Controls.Add(this.lblResult);
            this.splitMain.Panel2MinSize = 80;
            this.splitMain.Size = new System.Drawing.Size(884, 465);
            this.splitMain.TabIndex = 2;
            //
            // lblArgs
            //
            this.lblArgs.Dock = System.Windows.Forms.DockStyle.Top;
            this.lblArgs.Location = new System.Drawing.Point(0, 0);
            this.lblArgs.Name = "lblArgs";
            this.lblArgs.Padding = new System.Windows.Forms.Padding(4, 4, 0, 0);
            this.lblArgs.Size = new System.Drawing.Size(884, 20);
            this.lblArgs.TabIndex = 0;
            this.lblArgs.Text = "Arguments (JSON):";
            //
            // txtArgs
            //
            this.txtArgs.AcceptsReturn = true;
            this.txtArgs.AcceptsTab = true;
            this.txtArgs.Dock = System.Windows.Forms.DockStyle.Fill;
            this.txtArgs.Font = new System.Drawing.Font("Consolas", 9.5F);
            this.txtArgs.Location = new System.Drawing.Point(0, 20);
            this.txtArgs.Multiline = true;
            this.txtArgs.Name = "txtArgs";
            this.txtArgs.ScrollBars = System.Windows.Forms.ScrollBars.Both;
            this.txtArgs.Size = new System.Drawing.Size(884, 180);
            this.txtArgs.TabIndex = 1;
            this.txtArgs.WordWrap = false;
            //
            // lblResult
            //
            this.lblResult.Dock = System.Windows.Forms.DockStyle.Top;
            this.lblResult.Location = new System.Drawing.Point(0, 0);
            this.lblResult.Name = "lblResult";
            this.lblResult.Padding = new System.Windows.Forms.Padding(4, 4, 0, 0);
            this.lblResult.Size = new System.Drawing.Size(884, 20);
            this.lblResult.TabIndex = 0;
            this.lblResult.Text = "Response:";
            //
            // txtResult
            //
            this.txtResult.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(250)))), ((int)(((byte)(250)))), ((int)(((byte)(250)))));
            this.txtResult.Dock = System.Windows.Forms.DockStyle.Fill;
            this.txtResult.Font = new System.Drawing.Font("Consolas", 9.5F);
            this.txtResult.Location = new System.Drawing.Point(0, 20);
            this.txtResult.Multiline = true;
            this.txtResult.Name = "txtResult";
            this.txtResult.ReadOnly = true;
            this.txtResult.ScrollBars = System.Windows.Forms.ScrollBars.Both;
            this.txtResult.Size = new System.Drawing.Size(884, 241);
            this.txtResult.TabIndex = 1;
            this.txtResult.WordWrap = false;
            //
            // MainForm
            //
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(884, 613);
            this.Controls.Add(this.splitMain);
            this.Controls.Add(this.pnlTool);
            this.Controls.Add(this.pnlTop);
            this.MinimumSize = new System.Drawing.Size(640, 480);
            this.Name = "MainForm";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "aa Mcp Server - Test Client";
            this.pnlTop.ResumeLayout(false);
            this.pnlTop.PerformLayout();
            this.pnlTool.ResumeLayout(false);
            this.pnlTool.PerformLayout();
            this.splitMain.Panel1.ResumeLayout(false);
            this.splitMain.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.splitMain)).EndInit();
            this.splitMain.ResumeLayout(false);
            this.ResumeLayout(false);
        }

        #endregion
    }
}
