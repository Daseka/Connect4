using System.Text;

namespace Connect4.GameParts;

public class GameBoard
{
    public const int Columns = 7;
    public const int Rows = 6;
    private const int CellStates = 3;
    private const int LastToPlay = 1;

    public int[,] Board { get; private set; }
    public Player LastPlayed { get; private set; } = Player.None;

    public GameBoard()
    {
        Board = new int[Rows, Columns];
    }

    public GameBoard Copy()
    {
        var newBoard = new GameBoard();
        for (int i = 0; i < Rows; i++)
        {
            for (int j = 0; j < Columns; j++)
            {
                newBoard.Board[i, j] = Board[i, j];
            }
        }

        return newBoard;
    }

    public void DrawBoard(Graphics g)
    {
        int cellWidth = 50;
        int cellHeight = 50;

        int paddingLeft = 150;
        int paddingTop = 10;

        for (int i = 0; i < Rows; i++)
        {
            for (int j = 0; j < Columns; j++)
            {
                Brush brush = Board[i, j] switch
                {
                    1 => Brushes.Red,
                    2 => Brushes.Yellow,
                    _ => Brushes.LightGray
                };

                g.FillEllipse(brush, paddingLeft + j * cellWidth, paddingTop + i * cellHeight, cellWidth, cellHeight);
            }
        }
    }

    public int[] GetAvailableColumns()
    {
        var availableColumns = new List<int>();
        for (int col = 0; col < Columns; col++)
        {
            if (Board[0, col] == 0)
            {
                availableColumns.Add(col);
            }
        }

        return [.. availableColumns];
    }

    public string StateToString()
    {
        var data = new char[Board.Length * CellStates + LastToPlay];
        const char filled = '1';
        Array.Fill(data, '0');

        int index = 0;
        foreach (var item in Board)
        {
            if (item == (int)Player.Red)
            {
                data[index] = filled;
            }
            else if (item == (int)Player.Yellow)
            {
                data[index + Board.Length] = filled;
            }
            else /*if empty spot*/
            {
                data[index + Board.Length * 2] = filled;
            }

            index++;
        }

        data[^1] = LastPlayed == Player.Red ? '1' : '0';

        return new string(data);
    }

    public int[] StateToArray()
    {
        var data = new int[Board.Length * CellStates + LastToPlay];

        int index = 0;
        foreach (var item in Board)
        {
            if (item == (int)Player.Red)
            {
                data[index] = 1;
            }
            else if (item == (int)Player.Yellow)
            {
                data[index + Board.Length] = 1;
            }
            else /*if empty spot*/
            {
                data[index + Board.Length * 2] = 1;
            }
            index++;
        }

        data[^1] = LastPlayed == Player.Red ? 1 : 0;
        
        return data;
    }

    public bool HasWon(int player)
    {
        // Check horizontal, vertical, and diagonal connections
        return CheckHorizontal(player) || CheckVertical(player) || CheckDiagonal(player);
    }

    public bool PlacePiece(int column, int player)
    {
        if (column is < 0 or >= Columns)
        {
            return false;
        }

        for (int row = Rows - 1; row >= 0; row--)
        {
            if (Board[row, column] == 0)
            {
                Board[row, column] = player;
                LastPlayed = (Player)player;

                return true;
            }
        }

        return false;
    }

    public bool PlacePieceClick(MouseEventArgs? clickEvent, PictureBox pictureBox, int player)
    {
        int column = GetColumnFromClick(clickEvent, pictureBox);

        return PlacePiece(column, player);
    }

    public void ResetBoard()
    {
        for (int i = 0; i < Rows; i++)
        {
            for (int j = 0; j < Columns; j++)
            {
                Board[i, j] = 0;
            }
        }
    }

    private static int GetColumnFromClick(MouseEventArgs? clickEvent, PictureBox pictureBox1)
    {
        if (clickEvent == null)
        {
            return -1; // Return an invalid index if the event is null
        }

        // Adjust the click position to account for padding and calculate the column index
        const int emptySpace = 300;
        const int leftPadding = 150;
        int columnWidth = (pictureBox1.Width - emptySpace) / Columns;
        int columnIndex = (clickEvent.X - leftPadding) / columnWidth;

        // Ensure the column index is within valid range
        return Math.Max(0, Math.Min(columnIndex, Columns - 1));
    }

    private bool CheckDiagonal(int player)
    {
        // Check for diagonal win from bottom-left to top-right
        for (int row = 0; row < Rows; row++)
        {
            for (int col = 0; col < Columns; col++)
            {
                if (CheckDiagonalWin(row, col, player))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private bool CheckDiagonalWin(int row, int col, int player)
    {
        // Check for diagonal win from bottom-left to top-right
        int count = 0;
        for (int i = -3; i <= 3; i++)
        {
            int r = row + i;
            int c = col + i;
            if (r >= 0 && r < Rows && c >= 0 && c < Columns && Board[r, c] == player)
            {
                count++;
                if (count == 4)
                {
                    return true;
                }
            }
            else
            {
                count = 0;
            }
        }

        // Check for diagonal win from top-left to bottom-right
        count = 0;
        for (int i = -3; i <= 3; i++)
        {
            int r = row - i;
            int c = col + i;
            if (r >= 0 && r < Rows && c >= 0 && c < Columns && Board[r, c] == player)
            {
                count++;
                if (count == 4)
                {
                    return true;
                }
            }
            else
            {
                count = 0;
            }
        }

        return false;
    }

    private bool CheckHorizontal(int player)
    {
        // Iterate through each row of the board
        for (int row = 0; row < Rows; row++)
        {
            int count = 0;
            // Check each column in the current row
            for (int col = 0; col < Columns; col++)
            {
                // If the current cell matches the player's number, increment the count
                if (Board[row, col] == player)
                {
                    count++;
                    // If we have 4 in a row, return true
                    if (count == 4)
                    {
                        return true;
                    }
                }
                else
                {
                    // Reset count if the sequence is broken
                    count = 0;
                }
            }
        }
        // No horizontal win found
        return false;
    }

    private bool CheckVertical(int player)
    {
        // Check each column for a vertical win
        for (int col = 0; col < Columns; col++)
        {
            int count = 0;
            for (int row = 0; row < Rows; row++)
            {
                if (Board[row, col] == player)
                {
                    count++;
                    // If we have 4 in a row, return true
                    if (count == 4)
                    {
                        return true;
                    }
                }
                else
                {
                    count = 0; // Reset count if the sequence is broken
                }
            }
        }

        return false; // No vertical win found
    }
}