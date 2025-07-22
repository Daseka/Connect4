namespace Connect4.GameParts;

public class Connect4Game
{
    public int CurrentPlayer { get; private set; }
    public GameBoard GameBoard { get; }
    public Winner Winner { get; private set; } = Winner.StillPlaying;
    public Connect4Game()
    {
        GameBoard = new GameBoard();
        CurrentPlayer = (int)Player.Red; 
    }

    public void DrawBoard(Graphics g)
    {
        GameBoard.DrawBoard(g);
    }

    public int PlacePieceClick(MouseEventArgs? clickEvent, PictureBox pictureBox)
    {

        if (GameBoard.PlacePieceClick(clickEvent, pictureBox, CurrentPlayer))
        {
            if (HasWinner())
            {
                return CurrentPlayer;
            }

            CurrentPlayer = CurrentPlayer = FlipPlayer(CurrentPlayer);
        }

        return 0;
    }

    public int PlacePieceColumn(int column)
    {
        if (GameBoard.PlacePiece(column, CurrentPlayer))
        {
            if (HasWinner())
            {
                return CurrentPlayer;
            }

            CurrentPlayer = FlipPlayer(CurrentPlayer);
        }

        return 0;
    }

    public void SetState(string state)
    {
        GameBoard.StringToState(state);
    }

    public void ResetGame()
    {
        GameBoard.ResetBoard();
        CurrentPlayer = (int)Player.Red;
    }

    private static int FlipPlayer(int player)
    {
        return player == (int)Player.Red ? (int)Player.Yellow : (int)Player.Red;
    }

    private bool HasWinner()
    {
        bool hasWinner = GameBoard.HasWon(CurrentPlayer);

        if (hasWinner)
        {
            Winner = (Winner)CurrentPlayer;
            return true;
        }

        // Check if the board is full
        if (GameBoard.GetAvailableColumns().Length == 0)
        {
            Winner = Winner.Draw;
            return false;
        }

        return false;
    }
}
