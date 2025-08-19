using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace Connect4.GameParts
{
    public class SimpleChart : Control
    {
        private readonly List<double> _redPoints = new();
        private readonly List<double> _yellowPoints = new();
        private readonly List<double> _drawPoints = new();
        private readonly Pen _redPen = new(Color.Silver, 2);
        private readonly Pen _yellowPen = new(Color.FromArgb(101, 53, 1), 2);
        private readonly Pen _drawPen = new(Color.FromArgb(23, 82, 85), 2);
        private readonly Pen _gridPen = new(Color.Black);//FromArgb(30, 30, 30), 1);
        private readonly Brush _backgroundBrush = new SolidBrush(Color.Black);
        private readonly Brush _textBrush = new SolidBrush(Color.White);
        private readonly Font _titleFont = new("Arial", 12, FontStyle.Bold);
        private readonly Font _axisFont = new("Arial", 9);
        
        public string Title { get; set; } = "Chart";
        public string XAxisLabel { get; set; } = "X";
        public string YAxisLabel { get; set; } = "Y";
        public double YMin { get; set; } = 0;
        public double YMax { get; set; } = 100;
        public double DeepLearnThreshold { get; set; } = 55;

        public SimpleChart()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.DoubleBuffer, true);
            BackColor = Color.Black;
        }

        public void AddDataPoint(double redValue, double yellowValue, double drawValue )
        {
            _redPoints.Add(redValue);
            _yellowPoints.Add(yellowValue);
            _drawPoints.Add(drawValue);
            Invalidate();
        }

        public void ClearData()
        {
            _redPoints.Clear();
            _yellowPoints.Clear();
            _drawPoints.Clear();
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
            int rightMargin = 40;
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
                int redY;
                if (YMax == YMin)
                {
                    // If YMax == YMin, distribute grid lines evenly
                    redY = chartArea.Bottom - (int)((double)i / gridLines * chartArea.Height);
                }
                else
                {
                    redY = chartArea.Bottom - (int)((value - YMin) / (YMax - YMin) * chartArea.Height);
                }
                
                g.DrawLine(_gridPen, chartArea.Left, redY, chartArea.Right, redY);
                
                string label = value.ToString("F0");
                SizeF labelSize = g.MeasureString(label, _axisFont);
                g.DrawString(label, _axisFont, _textBrush, chartArea.Left - labelSize.Width - 5, redY - labelSize.Height / 2);
                g.DrawString(label, _axisFont, _textBrush, chartArea.Left + chartArea.Width + 30 - labelSize.Width, redY - labelSize.Height / 2);
            }

            // Draw data points and lines
            if (_redPoints.Count > 0)
            {
                Point[] redPoints = new Point[_redPoints.Count];
                Point[] yellowPoints = new Point[_yellowPoints.Count];
                Point[] drawPoints = new Point[_drawPoints.Count];
                for (int i = 0; i < _redPoints.Count; i++)
                {
                    double x;
                    if (_redPoints.Count == 1)
                    {
                        // Single data point - center it
                        x = chartArea.Left + chartArea.Width / 2.0;
                    }
                    else
                    {
                        // Multiple data points - distribute evenly
                        x = chartArea.Left + (double)i / (_redPoints.Count - 1) * chartArea.Width;
                    }
                    
                    double redY;
                    double yellowY;
                    double drawY;
                    if (YMax == YMin)
                    {
                        // If YMax == YMin, center the point vertically
                        redY = chartArea.Top + chartArea.Height / 2.0;
                        yellowY = chartArea.Top + chartArea.Height / 2.0;
                        drawY = chartArea.Top + chartArea.Height / 2.0;
                    }
                    else
                    {
                        redY = chartArea.Bottom - (_redPoints[i] - YMin) / (YMax - YMin) * chartArea.Height;
                        yellowY = chartArea.Bottom - (_yellowPoints[i] - YMin) / (YMax - YMin) * chartArea.Height;
                        drawY = chartArea.Bottom - (_drawPoints[i] - YMin) / (YMax - YMin) * chartArea.Height;
                    }
                    
                    redPoints[i] = new Point((int)x, (int)redY);
                    yellowPoints[i] = new Point((int)x, (int)yellowY);
                    drawPoints[i] = new Point((int)x, (int)drawY);
                }
                
                // Draw lines between points (only if we have more than 1 point)
                if (redPoints.Length > 1)
                {
                    g.DrawLines(_redPen, redPoints);
                    g.DrawLines(_yellowPen, yellowPoints);
                    g.DrawLines(_drawPen, drawPoints);
                }
                
                // Draw data points
                for (int i = 0; i < redPoints.Length; i++)
                {
                    // Choose color based on value - green if > 60, red otherwise
                    Color pointColor = _redPoints[i] >= DeepLearnThreshold
                        ? Color.FromArgb(0, 255, 0) 
                        : Color.FromArgb(255, 0, 0);
                    g.FillEllipse(new SolidBrush(pointColor), redPoints[i].X - 3, redPoints[i].Y - 3, 6, 6);
                }
            }
            
            // Draw X-axis labels
            if (_redPoints.Count > 0)
            {
                int maxLabels = Math.Min(10, _redPoints.Count);
                for (int i = 0; i < maxLabels; i++)
                {
                    int dataIndex = maxLabels == 1 
                        ? 0 
                        : i * (_redPoints.Count - 1) / (maxLabels - 1);

                    if (dataIndex >= 0 && dataIndex < _redPoints.Count)
                    {
                        double x;
                        if (_redPoints.Count == 1)
                        {
                            // Single data point - center it
                            x = chartArea.Left + chartArea.Width / 2.0;
                        }
                        else
                        {
                            // Multiple data points - distribute evenly
                            x = chartArea.Left + (double)dataIndex / (_redPoints.Count - 1) * chartArea.Width;
                        }
                        
                        string label = (dataIndex + 1).ToString();
                        SizeF labelSize = g.MeasureString(label, _axisFont);
                        g.DrawString(label, _axisFont, _textBrush, (float)x - labelSize.Width / 2, chartArea.Bottom + 5);
                    }
                }
            }
        }
        
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _redPen?.Dispose();
                _yellowPen?.Dispose();
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