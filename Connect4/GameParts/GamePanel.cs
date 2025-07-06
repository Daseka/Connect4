using System.Windows.Forms;

namespace Connect4.GameParts;

/// <summary>
/// A panel that displays a Connect4 game for parallel visualization
/// </summary>
public class GamePanel : Panel
{
    private readonly CompactConnect4Game _game;
    private readonly PictureBox _pictureBox;
    private readonly Label _statsLabel;
    
    public CompactConnect4Game Game => _game;
    public PictureBox PictureBox => _pictureBox;
    
    public int RedWins { get; private set; }
    public int YellowWins { get; private set; }
    public int Draws { get; private set; }
    public int GameId { get; }

    public GamePanel(int gameId)
    {
        GameId = gameId;
        _game = new CompactConnect4Game();
        
        // Create layout
        BorderStyle = BorderStyle.FixedSingle;
        Size = new Size(210, 240);
        Margin = new Padding(3);
        
        // Add stats label
        _statsLabel = new Label
        {
            Location = new Point(2, 2),
            Size = new Size(206, 20),
            Font = new Font(Font.FontFamily, 8),
            Text = $"Game #{gameId}: R:0 Y:0 D:0"
        };
        Controls.Add(_statsLabel);
        
        // Add picture box for the game board
        _pictureBox = new PictureBox
        {
            BackColor = Color.Black,
            Location = new Point(2, 24),
            Size = new Size(206, 212),
            Dock = DockStyle.None
        };
        _pictureBox.Paint += PictureBox_Paint;
        Controls.Add(_pictureBox);
    }

    private void PictureBox_Paint(object sender, PaintEventArgs e)
    {
        _game.DrawBoard(e.Graphics, _pictureBox.Width, _pictureBox.Height);
    }

    public void UpdateStats()
    {
        _statsLabel.Text = $"Game #{GameId}: R:{RedWins} Y:{YellowWins} D:{Draws}";
    }

    public void RecordResult(Winner winner)
    {
        switch (winner)
        {
            case Winner.Red:
                RedWins++;
                break;
            case Winner.Yellow:
                YellowWins++;
                break;
            case Winner.Draw:
                Draws++;
                break;
        }
        
        UpdateStats();
    }
}
