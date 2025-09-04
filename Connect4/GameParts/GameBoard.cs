using System.Text;

namespace Connect4.GameParts;

public class GameBoard
{
    public const int Columns = 7;
    public const int Rows = 6;
    private const int CellStates = 3;
    private const int LastToPlay = 1;
    const int LeftPadding = 50;
    private const int CellHeight = 50;
    private const int CellWidth = 50;

    public int[,] Board { get; private set; }
    public Player LastPlayed { get; private set; } = Player.None;

    private int [] _playedLocation = [-1,-1];

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

        newBoard.LastPlayed = LastPlayed;   

        return newBoard;
    }

    public void DrawBoard(Graphics g)
    {
        int paddingLeft = LeftPadding;
        int paddingTop = 10;

        for (int i = 0; i < Rows; i++)
        {
            for (int j = 0; j < Columns; j++)
            {
                Brush brush = Board[i, j] switch
                {
                    1 => Brushes.Red,
                    2 => Brushes.Yellow,
                    _ => new SolidBrush(Color.FromArgb(30, 30, 30))
                };

                if (_playedLocation[0] == i && _playedLocation[1] == j)
                {
                    g.FillEllipse(Brushes.Blue, paddingLeft - 4 + j * (CellWidth), paddingTop - 4 + i * (CellHeight), CellWidth + 10, CellHeight + 10);
                }

                g.FillEllipse(brush, paddingLeft + j * CellWidth, paddingTop + i * CellHeight, CellWidth, CellHeight);
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

    public void StringToState(string statestring)
    {
        var temp = BitKey.ToArray(statestring);
        statestring = string.Join("", temp.Select(x => x.ToString()));

        if (string.IsNullOrEmpty(statestring) || statestring.Length != Board.Length * CellStates + LastToPlay)
        {
            throw new ArgumentException("Invalid state string length.", nameof(statestring));
        }

        // Reset board
        for (int i = 0; i < Rows; i++)
        {
            for (int j = 0; j < Columns; j++)
            {
                Board[i, j] = 0;
            }
        }

        int boardSize = Board.Length;

        for (int idx = 0; idx < boardSize; idx++)
        {
            if (statestring[idx] == '1')
                Board[idx / Columns, idx % Columns] = (int)Player.Red;
            else if (statestring[idx + boardSize] == '1')
                Board[idx / Columns, idx % Columns] = (int)Player.Yellow;
            else if (statestring[idx + boardSize * 2] == '1')
                Board[idx / Columns, idx % Columns] = (int)Player.None;
        }

        LastPlayed = statestring[^1] == '1' ? Player.Red : Player.Yellow;
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
        return CheckHorizontal(player) || CheckVertical(player) || CheckDiagonal(player);
    }

    public bool HasDraw()
    {
        for (int col = 0; col < Columns; col++)
        {
            if (Board[0, col] == 0) 
            {
                return false;
            }
        }

        return !HasWon((int)Player.Red) && !HasWon((int)Player.Yellow);
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
                _playedLocation = [row, column];

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

        _playedLocation = [-1, -1];
        LastPlayed = Player.None;
    }

    private static int GetColumnFromClick(MouseEventArgs? clickEvent, PictureBox pictureBox1)
    {
        if (clickEvent == null)
        {
            return -1; 
        }

        int columnIndex = Math.Min((clickEvent.X - LeftPadding) / CellWidth, (CellWidth * Columns) / CellWidth);

        return Math.Max(0, Math.Min(columnIndex, Columns - 1));
    }

    private bool CheckDiagonal(int player)
    {
        for (int i = 0; i < Rows; i++)
        {
            for (int j = 0; j < Columns; j++)
            {
                if (CheckDiagonalWin(i, j, player))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private bool CheckDiagonalWin(int row, int column, int player)
    {
        int count = 0;
        for (int i = -3; i <= 3; i++)
        {
            int rowIndex = row + i;
            int columnIndex = column + i;
            if (rowIndex >= 0 
                && rowIndex < Rows 
                && columnIndex >= 0 
                && columnIndex < Columns 
                && Board[rowIndex, columnIndex] == player)
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

        count = 0;
        for (int i = -3; i <= 3; i++)
        {
            int rowIndex = row - i;
            int columnIndex = column + i;
            if (rowIndex >= 0 
                && rowIndex < Rows 
                && columnIndex >= 0 
                && columnIndex < Columns 
                && Board[rowIndex, columnIndex] == player)
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
        for (int row = 0; row < Rows; row++)
        {
            int count = 0;
            for (int column = 0; column < Columns; column++)
            {
                if (Board[row, column] == player)
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
        }

        return false;
    }

    private bool CheckVertical(int player)
    {
        for (int column = 0; column < Columns; column++)
        {
            int count = 0;
            for (int row = 0; row < Rows; row++)
            {
                if (Board[row, column] == player)
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
        }

        return false;
    }
}