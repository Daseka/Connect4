namespace Connect4.GameParts;

[Serializable]
public class BoardStateHistoricInfo(string boardState)
{
    public string BoardState { get; } = boardState;
    public int PlayerLastPlayed { get; set; } = -1;
    public int Draws { get; set; } = 0;
    public int RedWins { get; set; } = 0;
    public int YellowWins { get; set; } = 0;
    public double[] Policy { get; set; } = []; 
}
