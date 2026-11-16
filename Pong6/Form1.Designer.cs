namespace Pong;

partial class FormPong
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
            this.components = new System.ComponentModel.Container();
            this.pictureBoxDisplay = new System.Windows.Forms.PictureBox();
            this.timer1 = new System.Windows.Forms.Timer(this.components);
            this.pictureBoxLearningError = new System.Windows.Forms.PictureBox();
            this.pictureBoxFrame1 = new System.Windows.Forms.PictureBox();
            ((System.ComponentModel.ISupportInitialize)(this.pictureBoxDisplay)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.pictureBoxLearningError)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.pictureBoxFrame1)).BeginInit();
            this.SuspendLayout();
            // 
            // pictureBoxDisplay
            // 
            this.pictureBoxDisplay.Location = new System.Drawing.Point(0, 0);
            this.pictureBoxDisplay.Name = "pictureBoxDisplay";
            this.pictureBoxDisplay.Size = new System.Drawing.Size(429, 262);
            this.pictureBoxDisplay.TabIndex = 0;
            this.pictureBoxDisplay.TabStop = false;
            this.pictureBoxDisplay.MouseMove += new System.Windows.Forms.MouseEventHandler(this.PictureBoxDisplay_MouseMove);
            // 
            // timer1
            // 
            this.timer1.Interval = 5;
            this.timer1.Tick += new System.EventHandler(this.Timer1_Tick);
            // 
            // pictureBoxLearningError
            // 
            this.pictureBoxLearningError.BackColor = System.Drawing.Color.Black;
            this.pictureBoxLearningError.Location = new System.Drawing.Point(435, 0);
            this.pictureBoxLearningError.Name = "pictureBoxLearningError";
            this.pictureBoxLearningError.Size = new System.Drawing.Size(262, 262);
            this.pictureBoxLearningError.TabIndex = 1;
            this.pictureBoxLearningError.TabStop = false;
            // 
            // pictureBoxFrame1
            // 
            this.pictureBoxFrame1.BackColor = System.Drawing.Color.Black;
            this.pictureBoxFrame1.Location = new System.Drawing.Point(703, 0);
            this.pictureBoxFrame1.Name = "pictureBoxFrame1";
            this.pictureBoxFrame1.Size = new System.Drawing.Size(250, 262);
            this.pictureBoxFrame1.TabIndex = 3;
            this.pictureBoxFrame1.TabStop = false;
            // 
            // FormPong
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(957, 263);
            this.Controls.Add(this.pictureBoxFrame1);
            this.Controls.Add(this.pictureBoxLearningError);
            this.Controls.Add(this.pictureBoxDisplay);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedSingle;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "FormPong";
            this.SizeGripStyle = System.Windows.Forms.SizeGripStyle.Hide;
            this.Text = "Pong 4.0";
            this.KeyDown += new System.Windows.Forms.KeyEventHandler(this.FormPong_KeyDown);
            ((System.ComponentModel.ISupportInitialize)(this.pictureBoxDisplay)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.pictureBoxLearningError)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.pictureBoxFrame1)).EndInit();
            this.ResumeLayout(false);

    }

    #endregion

    private PictureBox pictureBoxDisplay;
    private System.Windows.Forms.Timer timer1;
    private PictureBox pictureBoxLearningError;
    private PictureBox pictureBoxFrame1;
}