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
            tabControl1 = new CustomTabControl();
            tabPage1 = new TabPage();
            pictureBox1 = new PictureBox();
            listBox1 = new ListBox();
            button1 = new Button();
            button4 = new Button();
            button5 = new Button();
            button6 = new Button();
            winPercentChart = new Connect4.GameParts.SimpleChart();
            flowLayoutPanel1 = new FlowLayoutPanel();
            tabPage2 = new TabPage();
            textBox1 = new TextBox();
            pictureBox2 = new PictureBox();
            button7 = new Button();
            label1 = new Label();
            tabPage3 = new TabPage();
            statusStrip1 = new StatusStrip();
            toolStripStatusLabel1 = new ToolStripStatusLabel();
            tabControl1.SuspendLayout();
            tabPage1.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)pictureBox1).BeginInit();
            tabPage2.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)pictureBox2).BeginInit();
            statusStrip1.SuspendLayout();
            SuspendLayout();
            // 
            // tabControl1
            // 
            tabControl1.Controls.Add(tabPage1);
            tabControl1.Controls.Add(tabPage2);
            tabControl1.Controls.Add(tabPage3);
            tabControl1.Dock = DockStyle.Fill;
            tabControl1.DrawMode = TabDrawMode.OwnerDrawFixed;
            tabControl1.Location = new Point(0, 0);
            tabControl1.Name = "tabControl1";
            tabControl1.SelectedIndex = 0;
            tabControl1.Size = new Size(1364, 859);
            tabControl1.TabIndex = 12;
            // 
            // tabPage1
            // 
            tabPage1.Controls.Add(pictureBox1);
            tabPage1.Controls.Add(listBox1);
            tabPage1.Controls.Add(button1);
            tabPage1.Controls.Add(button4);
            tabPage1.Controls.Add(button5);
            tabPage1.Controls.Add(button6);
            tabPage1.Controls.Add(winPercentChart);
            tabPage1.Controls.Add(flowLayoutPanel1);
            tabPage1.Location = new Point(4, 25);
            tabPage1.Name = "tabPage1";
            tabPage1.Padding = new Padding(3);
            tabPage1.Size = new Size(1356, 830);
            tabPage1.TabIndex = 0;
            tabPage1.Text = "Game";
            tabPage1.UseVisualStyleBackColor = true;
            // 
            // pictureBox1
            // 
            pictureBox1.BackColor = Color.Black;
            pictureBox1.Location = new Point(6, 36);
            pictureBox1.Name = "pictureBox1";
            pictureBox1.Size = new Size(613, 328);
            pictureBox1.TabIndex = 0;
            pictureBox1.TabStop = false;
            // 
            // listBox1
            // 
            listBox1.FormattingEnabled = true;
            listBox1.ItemHeight = 15;
            listBox1.Location = new Point(700, 36);
            listBox1.Name = "listBox1";
            listBox1.ScrollAlwaysVisible = true;
            listBox1.Size = new Size(626, 319);
            listBox1.TabIndex = 2;
            // 
            // button1
            // 
            button1.Location = new Point(700, 11);
            button1.Name = "button1";
            button1.Size = new Size(75, 23);
            button1.TabIndex = 3;
            button1.Text = "Reset game";
            button1.UseVisualStyleBackColor = true;
            button1.Click += ResetButton_Click;
            // 
            // button4
            // 
            button4.Location = new Point(1020, 11);
            button4.Name = "button4";
            button4.Size = new Size(100, 23);
            button4.TabIndex = 6;
            button4.Text = "Self play";
            button4.UseVisualStyleBackColor = true;
            button4.Click += SelfPlayButton_Click;
            // 
            // button5
            // 
            button5.Location = new Point(1138, 11);
            button5.Name = "button5";
            button5.Size = new Size(75, 23);
            button5.TabIndex = 7;
            button5.Text = "Arena";
            button5.UseVisualStyleBackColor = true;
            button5.Click += Arena_Click;
            // 
            // button6
            // 
            button6.Location = new Point(1231, 11);
            button6.Name = "button6";
            button6.Size = new Size(95, 23);
            button6.TabIndex = 11;
            button6.Text = "Clear Chart";
            button6.UseVisualStyleBackColor = true;
            button6.Click += ClearChart_Click;
            // 
            // winPercentChart
            // 
            winPercentChart.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            winPercentChart.BackColor = Color.Black;
            winPercentChart.DeepLearnThreshold = 55D;
            winPercentChart.Location = new Point(6, 370);
            winPercentChart.Name = "winPercentChart";
            winPercentChart.Size = new Size(1318, 200);
            winPercentChart.TabIndex = 10;
            winPercentChart.Text = "Win Rate History";
            winPercentChart.Title = "Chart";
            winPercentChart.XAxisLabel = "X";
            winPercentChart.YAxisLabel = "Y";
            winPercentChart.YMax = 100D;
            winPercentChart.YMin = 0D;
            // 
            // flowLayoutPanel1
            // 
            flowLayoutPanel1.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            flowLayoutPanel1.AutoScroll = true;
            flowLayoutPanel1.BackColor = SystemColors.Control;
            flowLayoutPanel1.BorderStyle = BorderStyle.Fixed3D;
            flowLayoutPanel1.Location = new Point(6, 593);
            flowLayoutPanel1.Name = "flowLayoutPanel1";
            flowLayoutPanel1.Padding = new Padding(5);
            flowLayoutPanel1.Size = new Size(1347, 231);
            flowLayoutPanel1.TabIndex = 8;
            // 
            // tabPage2
            // 
            tabPage2.Controls.Add(textBox1);
            tabPage2.Controls.Add(pictureBox2);
            tabPage2.Controls.Add(button7);
            tabPage2.Controls.Add(label1);
            tabPage2.Location = new Point(4, 25);
            tabPage2.Name = "tabPage2";
            tabPage2.Padding = new Padding(3);
            tabPage2.Size = new Size(1356, 830);
            tabPage2.TabIndex = 1;
            tabPage2.Text = "State Reader";
            tabPage2.UseVisualStyleBackColor = true;
            // 
            // textBox1
            // 
            textBox1.Location = new Point(952, 34);
            textBox1.Name = "textBox1";
            textBox1.Size = new Size(223, 23);
            textBox1.TabIndex = 4;
            // 
            // pictureBox2
            // 
            pictureBox2.BackColor = Color.Black;
            pictureBox2.Location = new Point(6, 6);
            pictureBox2.Name = "pictureBox2";
            pictureBox2.Size = new Size(613, 356);
            pictureBox2.TabIndex = 0;
            pictureBox2.TabStop = false;
            // 
            // button7
            // 
            button7.Location = new Point(1181, 36);
            button7.Name = "button7";
            button7.Size = new Size(120, 23);
            button7.TabIndex = 2;
            button7.Text = "Read board state";
            button7.UseVisualStyleBackColor = true;
            button7.Click += ReadBoardStateButton_Click;
            // 
            // label1
            // 
            label1.AutoSize = true;
            label1.ForeColor = Color.Black;
            label1.Location = new Point(872, 36);
            label1.Name = "label1";
            label1.Size = new Size(70, 15);
            label1.TabIndex = 3;
            label1.Text = "Board State:";
            // 
            // tabPage3
            // 
            tabPage3.Location = new Point(4, 25);
            tabPage3.Name = "tabPage3";
            tabPage3.Padding = new Padding(3);
            tabPage3.Size = new Size(1356, 830);
            tabPage3.TabIndex = 2;
            tabPage3.Text = "Single Game";
            tabPage3.UseVisualStyleBackColor = true;
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
            BackColor = Color.Black;
            ClientSize = new Size(1364, 881);
            Controls.Add(tabControl1);
            Controls.Add(statusStrip1);
            Name = "Form1";
            StartPosition = FormStartPosition.CenterScreen;
            Text = "Connect4";
            FormClosing += Form1_FormClosing;
            tabControl1.ResumeLayout(false);
            tabPage1.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)pictureBox1).EndInit();
            tabPage2.ResumeLayout(false);
            tabPage2.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)pictureBox2).EndInit();
            statusStrip1.ResumeLayout(false);
            statusStrip1.PerformLayout();
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion

        private CustomTabControl tabControl1;
        private TabPage tabPage1;
        private TabPage tabPage2;
        private TabPage tabPage3;
        private PictureBox pictureBox1;
        private ListBox listBox1;
        private Button button1;
        private Button button4;
        private Button button5;
        private Button button6;
        private PictureBox pictureBox2;
        private Button button7;
        private Label label1;
        private FlowLayoutPanel flowLayoutPanel1;
        private StatusStrip statusStrip1;
        private ToolStripStatusLabel toolStripStatusLabel1;
        private Connect4.GameParts.SimpleChart winPercentChart;
        private TextBox textBox1;
    }
}
