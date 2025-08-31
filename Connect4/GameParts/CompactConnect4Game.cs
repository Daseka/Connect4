namespace Connect4.GameParts;

public class CompactConnect4Game : Connect4Game
{
    public void DrawBoard(Graphics g, int width, int height)
    {
        int cellWidth = width / (GameBoard.Columns + 1);
        int cellHeight = height / (GameBoard.Rows + 1);
        
        int cellSize = Math.Min(cellWidth, cellHeight);
        
        int paddingLeft = (width - (cellSize * GameBoard.Columns)) / 2;
        int paddingTop = (height - (cellSize * GameBoard.Rows)) / 2;
        
        for (int i = 0; i < GameBoard.Rows; i++)
        {
            for (int j = 0; j < GameBoard.Columns; j++)
            {
                Brush brush = GameBoard.Board[i, j] switch
                {
                    1 => Brushes.Red,
                    2 => Brushes.Yellow,
                    _ => new SolidBrush(Color.FromArgb(10, 10, 10))
                };
                
                g.FillEllipse(
                    brush, 
                    paddingLeft + j * cellSize, 
                    paddingTop + i * cellSize, 
                    cellSize - 2, 
                    cellSize - 2
                );
            }
        }
    }
}