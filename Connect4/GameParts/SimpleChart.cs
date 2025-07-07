using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace Connect4.GameParts
{
    public class SimpleChart : Control
    {
        private const int DeepLearnThreshold = 55;
        private readonly List<double> _dataPoints = new();
        private readonly Pen _linePen = new(Color.Silver, 2);
        private readonly Pen _gridPen = new(Color.FromArgb(30, 30, 30), 1);
        private readonly Brush _backgroundBrush = new SolidBrush(Color.Black);
        private readonly Brush _textBrush = new SolidBrush(Color.White);
        private readonly Font _titleFont = new("Arial", 12, FontStyle.Bold);
        private readonly Font _axisFont = new("Arial", 9);
        
        public string Title { get; set; } = "Chart";
        public string XAxisLabel { get; set; } = "X";
        public string YAxisLabel { get; set; } = "Y";
        public double YMin { get; set; } = 0;
        public double YMax { get; set; } = 100;

        public SimpleChart()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.DoubleBuffer, true);
            BackColor = Color.Black;
        }

        public void AddDataPoint(double value)
        {
            _dataPoints.Add(value);
            Invalidate();
        }

        public void ClearData()
        {
            _dataPoints.Clear();
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            
            Graphics g = e.Graphics;
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            
            // Fill background
            g.FillRectangle(_backgroundBrush, ClientRectangle);
            
            // Calculate margins
            int leftMargin = 60;
            int rightMargin = 20;
            int topMargin = 50;
            int bottomMargin = 50;
            
            Rectangle chartArea = new Rectangle(
                leftMargin,
                topMargin,
                Width - leftMargin - rightMargin,
                Height - topMargin - bottomMargin
            );
            
            // Draw title
            SizeF titleSize = g.MeasureString(Title, _titleFont);
            g.DrawString(Title, _titleFont, _textBrush, 
                (Width - titleSize.Width) / 2, 10);
            
            // Draw Y-axis label (rotated)
            g.TranslateTransform(15, chartArea.Y + chartArea.Height / 2);
            g.RotateTransform(-90);
            SizeF yLabelSize = g.MeasureString(YAxisLabel, _axisFont);
            g.DrawString(YAxisLabel, _axisFont, _textBrush, -yLabelSize.Width / 2, 0);
            g.ResetTransform();
            
            // Draw X-axis label
            SizeF xLabelSize = g.MeasureString(XAxisLabel, _axisFont);
            g.DrawString(XAxisLabel, _axisFont, _textBrush, 
                chartArea.X + (chartArea.Width - xLabelSize.Width) / 2, 
                chartArea.Bottom + 30);
            
            // Draw chart border
            g.DrawRectangle(Pens.LightGray, chartArea);
            
            // Draw grid lines and Y-axis labels
            int gridLines = 5;
            for (int i = 0; i <= gridLines; i++)
            {
                double value = YMin + (YMax - YMin) * i / gridLines;
                int y;
                if (YMax == YMin)
                {
                    // If YMax == YMin, distribute grid lines evenly
                    y = chartArea.Bottom - (int)((double)i / gridLines * chartArea.Height);
                }
                else
                {
                    y = chartArea.Bottom - (int)((value - YMin) / (YMax - YMin) * chartArea.Height);
                }
                
                g.DrawLine(_gridPen, chartArea.Left, y, chartArea.Right, y);
                
                string label = value.ToString("F0");
                SizeF labelSize = g.MeasureString(label, _axisFont);
                g.DrawString(label, _axisFont, _textBrush, 
                    chartArea.Left - labelSize.Width - 5, y - labelSize.Height / 2);
            }
            
            // Draw data points and lines
            if (_dataPoints.Count > 0)
            {
                Point[] points = new Point[_dataPoints.Count];
                
                for (int i = 0; i < _dataPoints.Count; i++)
                {
                    double x;
                    if (_dataPoints.Count == 1)
                    {
                        // Single data point - center it
                        x = chartArea.Left + chartArea.Width / 2.0;
                    }
                    else
                    {
                        // Multiple data points - distribute evenly
                        x = chartArea.Left + (double)i / (_dataPoints.Count - 1) * chartArea.Width;
                    }
                    
                    double y;
                    if (YMax == YMin)
                    {
                        // If YMax == YMin, center the point vertically
                        y = chartArea.Top + chartArea.Height / 2.0;
                    }
                    else
                    {
                        y = chartArea.Bottom - (_dataPoints[i] - YMin) / (YMax - YMin) * chartArea.Height;
                    }
                    
                    points[i] = new Point((int)x, (int)y);
                }
                
                // Draw lines between points (only if we have more than 1 point)
                if (points.Length > 1)
                {
                    g.DrawLines(_linePen, points);
                }
                
                // Draw data points
                for (int i = 0; i < points.Length; i++)
                {
                    // Choose color based on value - green if > 60, red otherwise
                    Color pointColor = _dataPoints[i] > DeepLearnThreshold
                        ? Color.FromArgb(0, 255, 0) 
                        : Color.FromArgb(255, 0, 0);
                    g.FillEllipse(new SolidBrush(pointColor), points[i].X - 3, points[i].Y - 3, 6, 6);
                }
            }
            
            // Draw X-axis labels
            if (_dataPoints.Count > 0)
            {
                int maxLabels = Math.Min(10, _dataPoints.Count);
                for (int i = 0; i < maxLabels; i++)
                {
                    int dataIndex;
                    if (maxLabels == 1)
                    {
                        dataIndex = 0;
                    }
                    else
                    {
                        dataIndex = i * (_dataPoints.Count - 1) / (maxLabels - 1);
                    }
                    
                    if (dataIndex >= 0 && dataIndex < _dataPoints.Count)
                    {
                        double x;
                        if (_dataPoints.Count == 1)
                        {
                            // Single data point - center it
                            x = chartArea.Left + chartArea.Width / 2.0;
                        }
                        else
                        {
                            // Multiple data points - distribute evenly
                            x = chartArea.Left + (double)dataIndex / (_dataPoints.Count - 1) * chartArea.Width;
                        }
                        
                        string label = (dataIndex + 1).ToString();
                        SizeF labelSize = g.MeasureString(label, _axisFont);
                        g.DrawString(label, _axisFont, _textBrush, 
                            (float)x - labelSize.Width / 2, chartArea.Bottom + 5);
                    }
                }
            }
        }
        
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _linePen?.Dispose();
                _gridPen?.Dispose();
                _backgroundBrush?.Dispose();
                _textBrush?.Dispose();
                _titleFont?.Dispose();
                _axisFont?.Dispose();
            }
            base.Dispose(disposing);
        }
    }
} 