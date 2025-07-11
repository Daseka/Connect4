namespace Connect4
{
    partial class Form1
    {
        /// <summary>
        ///  Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        ///  Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise.</param>
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
            pictureBox1 = new PictureBox();
            checkBox1 = new CheckBox();
            listBox1 = new ListBox();
            button1 = new Button();
            button2 = new Button();
            button3 = new Button();
            button4 = new Button();
            button5 = new Button();
            button6 = new Button();
            flowLayoutPanel1 = new FlowLayoutPanel();
            statusStrip1 = new StatusStrip();
            toolStripStatusLabel1 = new ToolStripStatusLabel();
            winPercentChart = new Connect4.GameParts.SimpleChart();
            ((System.ComponentModel.ISupportInitialize)pictureBox1).BeginInit();
            statusStrip1.SuspendLayout();
            SuspendLayout();
            // 
            // pictureBox1
            // 
            pictureBox1.BackColor = Color.Black;
            pictureBox1.Location = new Point(12, 48);
            pictureBox1.Name = "pictureBox1";
            pictureBox1.Size = new Size(613, 356);
            pictureBox1.TabIndex = 0;
            pictureBox1.TabStop = false;
            // 
            // checkBox1
            // 
            checkBox1.AutoSize = true;
            checkBox1.Location = new Point(16, 23);
            checkBox1.Name = "checkBox1";
            checkBox1.Size = new Size(77, 19);
            checkBox1.TabIndex = 1;
            checkBox1.Text = "Auto Play";
            checkBox1.UseVisualStyleBackColor = true;
            // 
            // listBox1
            // 
            listBox1.FormattingEnabled = true;
            listBox1.ItemHeight = 15;
            listBox1.Location = new Point(706, 48);
            listBox1.Name = "listBox1";
            listBox1.ScrollAlwaysVisible = true;
            listBox1.Size = new Size(638, 349);
            listBox1.TabIndex = 2;
            // 
            // button1
            // 
            button1.Location = new Point(706, 23);
            button1.Name = "button1";
            button1.Size = new Size(75, 23);
            button1.TabIndex = 3;
            button1.Text = "Save";
            button1.UseVisualStyleBackColor = true;
            button1.Click += SaveButton_Click;
            // 
            // button2
            // 
            button2.Location = new Point(787, 23);
            button2.Name = "button2";
            button2.Size = new Size(75, 23);
            button2.TabIndex = 4;
            button2.Text = "Load";
            button2.UseVisualStyleBackColor = true;
            button2.Click += LoadButton_Click;
            // 
            // button3
            // 
            button3.Location = new Point(868, 23);
            button3.Name = "button3";
            button3.Size = new Size(75, 23);
            button3.TabIndex = 5;
            button3.Text = "Train";
            button3.UseVisualStyleBackColor = true;
            button3.Click += TrainButton_Click;
            // 
            // button4
            // 
            button4.Location = new Point(949, 23);
            button4.Name = "button4";
            button4.Size = new Size(100, 23);
            button4.TabIndex = 6;
            button4.Text = "Parallel Play";
            button4.UseVisualStyleBackColor = true;
            button4.Click += SelfPlayButton_Click;
            // 
            // button5
            // 
            button5.Location = new Point(1166, 23);
            button5.Name = "button5";
            button5.Size = new Size(75, 23);
            button5.TabIndex = 7;
            button5.Text = "Arena";
            button5.UseVisualStyleBackColor = true;
            button5.Click += Arena_Click;
            // 
            // button6
            // 
            button6.Location = new Point(1247, 23);
            button6.Name = "button6";
            button6.Size = new Size(95, 23);
            button6.TabIndex = 11;
            button6.Text = "Clear Chart";
            button6.UseVisualStyleBackColor = true;
            button6.Click += ClearChart_Click;
            // 
            // redPercentChart
            // 
            winPercentChart.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            winPercentChart.Location = new Point(12, 410);
            winPercentChart.Name = "percentChart";
            winPercentChart.Size = new Size(1332, 200);
            winPercentChart.TabIndex = 10;
            winPercentChart.Text = "Win Rate History";
            // 
            // flowLayoutPanel1
            // 
            flowLayoutPanel1.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            flowLayoutPanel1.AutoScroll = true;
            flowLayoutPanel1.BackColor = SystemColors.Control;
            flowLayoutPanel1.BorderStyle = BorderStyle.Fixed3D;
            flowLayoutPanel1.Location = new Point(12, 616);
            flowLayoutPanel1.Name = "flowLayoutPanel1";
            flowLayoutPanel1.Padding = new Padding(5);
            flowLayoutPanel1.Size = new Size(1332, 240);
            flowLayoutPanel1.TabIndex = 8;
            // 
            // statusStrip1
            // 
            statusStrip1.Items.AddRange(new ToolStripItem[] { toolStripStatusLabel1 });
            statusStrip1.Location = new Point(0, 859);
            statusStrip1.Name = "statusStrip1";
            statusStrip1.Size = new Size(1364, 22);
            statusStrip1.TabIndex = 9;
            statusStrip1.Text = "statusStrip1";
            // 
            // toolStripStatusLabel1
            // 
            toolStripStatusLabel1.Name = "toolStripStatusLabel1";
            toolStripStatusLabel1.Size = new Size(39, 17);
            toolStripStatusLabel1.Text = "Ready";
            // 
            // Form1
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(1364, 881);
            Controls.Add(statusStrip1);
            Controls.Add(flowLayoutPanel1);
            Controls.Add(winPercentChart);
            Controls.Add(button6);
            Controls.Add(button5);
            Controls.Add(button4);
            Controls.Add(button3);
            Controls.Add(button2);
            Controls.Add(button1);
            Controls.Add(listBox1);
            Controls.Add(checkBox1);
            Controls.Add(pictureBox1);
            Name = "Form1";
            Text = "Connect4";
            ((System.ComponentModel.ISupportInitialize)pictureBox1).EndInit();
            statusStrip1.ResumeLayout(false);
            statusStrip1.PerformLayout();
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion

        private PictureBox pictureBox1;
        private CheckBox checkBox1;
        private ListBox listBox1;
        private Button button1;
        private Button button2;
        private Button button3;
        private Button button4;
        private Button button5;
        private Button button6;
        private FlowLayoutPanel flowLayoutPanel1;
        private StatusStrip statusStrip1;
        private ToolStripStatusLabel toolStripStatusLabel1;
        private Connect4.GameParts.SimpleChart winPercentChart;
    }
}
