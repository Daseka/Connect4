namespace Connect4.GameParts;

/// <summary>
/// A specialized version of Connect4Game that uses scaled drawing for the smaller game panels
/// </summary>
public class CompactConnect4Game : Connect4Game
{
    /// <summary>
    /// Draw the board with custom dimensions to fit the container
    /// </summary>
    public void DrawBoard(Graphics g, int width, int height)
    {
        // Calculate the cell size based on available width and height
        int cellWidth = width / (GameBoard.Columns + 1);
        int cellHeight = height / (GameBoard.Rows + 1);
        
        // Use the smaller of the two to maintain square cells
        int cellSize = Math.Min(cellWidth, cellHeight);
        
        // Center the board in the available space
        int paddingLeft = (width - (cellSize * GameBoard.Columns)) / 2;
        int paddingTop = (height - (cellSize * GameBoard.Rows)) / 2;
        
        // Draw each cell
        for (int i = 0; i < GameBoard.Rows; i++)
        {
            for (int j = 0; j < GameBoard.Columns; j++)
            {
                Brush brush = GameBoard.Board[i, j] switch
                {
                    1 => Brushes.Red,
                    2 => Brushes.Yellow,
                    _ => Brushes.LightGray
                };
                
                g.FillEllipse(
                    brush, 
                    paddingLeft + j * cellSize, 
                    paddingTop + i * cellSize, 
                    cellSize - 2, // Subtract 2 for a small gap between circles
                    cellSize - 2
                );
            }
        }
    }
}