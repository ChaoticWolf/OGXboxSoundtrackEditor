﻿namespace OGXboxSoundtrackEditor
{
    partial class XBEPatch
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(XBEPatch));
            this.btnOk = new System.Windows.Forms.Button();
            this.labelInfo = new System.Windows.Forms.Label();
            this.btnBrowse = new System.Windows.Forms.Button();
            this.labelStatus = new System.Windows.Forms.Label();
            this.groupBox1 = new System.Windows.Forms.GroupBox();
            this.radioButtonE = new System.Windows.Forms.RadioButton();
            this.radioButtonG = new System.Windows.Forms.RadioButton();
            this.radioButtonF = new System.Windows.Forms.RadioButton();
            this.groupBox2 = new System.Windows.Forms.GroupBox();
            this.radioButtonHDD2 = new System.Windows.Forms.RadioButton();
            this.radioButtonHDD1 = new System.Windows.Forms.RadioButton();
            this.groupBox1.SuspendLayout();
            this.groupBox2.SuspendLayout();
            this.SuspendLayout();
            // 
            // btnOk
            // 
            this.btnOk.Location = new System.Drawing.Point(380, 171);
            this.btnOk.Name = "btnOk";
            this.btnOk.Size = new System.Drawing.Size(90, 30);
            this.btnOk.TabIndex = 0;
            this.btnOk.Text = "Ok";
            this.btnOk.UseVisualStyleBackColor = true;
            this.btnOk.Click += new System.EventHandler(this.btnOk_Click);
            // 
            // labelInfo
            // 
            this.labelInfo.AutoSize = true;
            this.labelInfo.Location = new System.Drawing.Point(8, 9);
            this.labelInfo.Name = "labelInfo";
            this.labelInfo.Size = new System.Drawing.Size(467, 64);
            this.labelInfo.TabIndex = 1;
            this.labelInfo.Text = resources.GetString("labelInfo.Text");
            // 
            // btnBrowse
            // 
            this.btnBrowse.Location = new System.Drawing.Point(22, 108);
            this.btnBrowse.Name = "btnBrowse";
            this.btnBrowse.Size = new System.Drawing.Size(90, 30);
            this.btnBrowse.TabIndex = 2;
            this.btnBrowse.Text = "Browse";
            this.btnBrowse.UseVisualStyleBackColor = true;
            this.btnBrowse.Click += new System.EventHandler(this.btnBrowse_Click);
            // 
            // labelStatus
            // 
            this.labelStatus.AutoSize = true;
            this.labelStatus.Location = new System.Drawing.Point(118, 115);
            this.labelStatus.Name = "labelStatus";
            this.labelStatus.Size = new System.Drawing.Size(100, 16);
            this.labelStatus.TabIndex = 3;
            this.labelStatus.Text = "No file selected";
            // 
            // groupBox1
            // 
            this.groupBox1.Controls.Add(this.radioButtonE);
            this.groupBox1.Controls.Add(this.radioButtonG);
            this.groupBox1.Controls.Add(this.radioButtonF);
            this.groupBox1.Location = new System.Drawing.Point(244, 86);
            this.groupBox1.Name = "groupBox1";
            this.groupBox1.Size = new System.Drawing.Size(130, 60);
            this.groupBox1.TabIndex = 4;
            this.groupBox1.TabStop = false;
            this.groupBox1.Text = "Partition";
            // 
            // radioButtonE
            // 
            this.radioButtonE.AutoSize = true;
            this.radioButtonE.Location = new System.Drawing.Point(6, 27);
            this.radioButtonE.Name = "radioButtonE";
            this.radioButtonE.Size = new System.Drawing.Size(37, 20);
            this.radioButtonE.TabIndex = 6;
            this.radioButtonE.TabStop = true;
            this.radioButtonE.Text = "E";
            this.radioButtonE.UseVisualStyleBackColor = true;
            // 
            // radioButtonG
            // 
            this.radioButtonG.AutoSize = true;
            this.radioButtonG.Location = new System.Drawing.Point(86, 27);
            this.radioButtonG.Name = "radioButtonG";
            this.radioButtonG.Size = new System.Drawing.Size(38, 20);
            this.radioButtonG.TabIndex = 1;
            this.radioButtonG.TabStop = true;
            this.radioButtonG.Text = "G";
            this.radioButtonG.UseVisualStyleBackColor = true;
            // 
            // radioButtonF
            // 
            this.radioButtonF.AutoSize = true;
            this.radioButtonF.Checked = true;
            this.radioButtonF.Location = new System.Drawing.Point(47, 27);
            this.radioButtonF.Name = "radioButtonF";
            this.radioButtonF.Size = new System.Drawing.Size(36, 20);
            this.radioButtonF.TabIndex = 0;
            this.radioButtonF.TabStop = true;
            this.radioButtonF.Text = "F";
            this.radioButtonF.UseVisualStyleBackColor = true;
            // 
            // groupBox2
            // 
            this.groupBox2.Controls.Add(this.radioButtonHDD2);
            this.groupBox2.Controls.Add(this.radioButtonHDD1);
            this.groupBox2.Location = new System.Drawing.Point(380, 86);
            this.groupBox2.Name = "groupBox2";
            this.groupBox2.Size = new System.Drawing.Size(80, 60);
            this.groupBox2.TabIndex = 5;
            this.groupBox2.TabStop = false;
            this.groupBox2.Text = "Drive";
            // 
            // radioButtonHDD2
            // 
            this.radioButtonHDD2.AutoSize = true;
            this.radioButtonHDD2.Location = new System.Drawing.Point(6, 34);
            this.radioButtonHDD2.Name = "radioButtonHDD2";
            this.radioButtonHDD2.Size = new System.Drawing.Size(68, 20);
            this.radioButtonHDD2.TabIndex = 1;
            this.radioButtonHDD2.Text = "HDD 2";
            this.radioButtonHDD2.UseVisualStyleBackColor = true;
            // 
            // radioButtonHDD1
            // 
            this.radioButtonHDD1.AutoSize = true;
            this.radioButtonHDD1.Checked = true;
            this.radioButtonHDD1.Location = new System.Drawing.Point(6, 14);
            this.radioButtonHDD1.Name = "radioButtonHDD1";
            this.radioButtonHDD1.Size = new System.Drawing.Size(68, 20);
            this.radioButtonHDD1.TabIndex = 0;
            this.radioButtonHDD1.TabStop = true;
            this.radioButtonHDD1.Text = "HDD 1";
            this.radioButtonHDD1.UseVisualStyleBackColor = true;
            // 
            // XBEPatch
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(8F, 16F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(482, 213);
            this.Controls.Add(this.groupBox2);
            this.Controls.Add(this.groupBox1);
            this.Controls.Add(this.labelStatus);
            this.Controls.Add(this.btnBrowse);
            this.Controls.Add(this.labelInfo);
            this.Controls.Add(this.btnOk);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedSingle;
            this.MaximizeBox = false;
            this.Name = "XBEPatch";
            this.ShowIcon = false;
            this.Text = "Patch XBE";
            this.groupBox1.ResumeLayout(false);
            this.groupBox1.PerformLayout();
            this.groupBox2.ResumeLayout(false);
            this.groupBox2.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Button btnOk;
        private System.Windows.Forms.Label labelInfo;
        private System.Windows.Forms.Button btnBrowse;
        private System.Windows.Forms.Label labelStatus;
        private System.Windows.Forms.GroupBox groupBox1;
        private System.Windows.Forms.RadioButton radioButtonG;
        private System.Windows.Forms.RadioButton radioButtonF;
        private System.Windows.Forms.GroupBox groupBox2;
        private System.Windows.Forms.RadioButton radioButtonHDD1;
        private System.Windows.Forms.RadioButton radioButtonHDD2;
        private System.Windows.Forms.RadioButton radioButtonE;
    }
}