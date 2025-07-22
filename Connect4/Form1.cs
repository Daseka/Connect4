using Connect4.Ais;
using Connect4.GameParts;
using DeepNetwork;

namespace Connect4;

public partial class Form1 : Form
{
    private readonly Connect4Game _connect4Game = new();
    private readonly Connect4Game _editorConnect4Game = new();
    private readonly List<GamePanel> _gamePanels = [];

    private CancellationTokenSource _cancellationTokenSource = new();
    private int _gamesPlayed = 0;
    private bool _isParallelSelfPlayRunning;
    private FlatDumbNetwork _newPolicyNetwork;
    private FlatDumbNetwork _newValueNetwork;
    private FlatDumbNetwork _oldPolicyNetwork;
    private FlatDumbNetwork _oldValueNetwork;
    private Mcts _redMcts;
    private Mcts _yellowMcts;

    public Form1()
    {
        InitializeComponent();

        pictureBox1.Size = new Size(650, 320);
        pictureBox1.Paint += PictureBox1_Paint;
        pictureBox1.Click += PictureBox1_Click;

        // Set up the second picture box for board editor
        pictureBox2.Size = new Size(650, 320);
        pictureBox2.Paint += PictureBox2_Paint;

        _oldValueNetwork = new FlatDumbNetwork([127, 196, 98, 49, 1]);
        _oldPolicyNetwork = new FlatDumbNetwork([127, 196, 98, 49, 7]);
        _newValueNetwork = new FlatDumbNetwork([127, 196, 98, 49, 1]);
        _newPolicyNetwork = new FlatDumbNetwork([127, 196, 98, 49, 7]);

        _oldValueNetwork = FlatDumbNetwork.CreateFromFile(OldValueNetwork) ?? _oldValueNetwork;
        _oldPolicyNetwork = FlatDumbNetwork.CreateFromFile(OldPolicyNetwork) ?? _oldPolicyNetwork;
        _oldPolicyNetwork.Trained = true;
        _oldValueNetwork.Trained = true;
        _yellowMcts = new Mcts(McstIterations, _oldValueNetwork, _oldPolicyNetwork);

        _newValueNetwork = FlatDumbNetwork.CreateFromFile(OldValueNetwork) ?? _newValueNetwork;
        _newPolicyNetwork = FlatDumbNetwork.CreateFromFile(OldPolicyNetwork) ?? _newPolicyNetwork;
        _newPolicyNetwork.Trained = true;
        _newValueNetwork.Trained = true;
        _redMcts = new Mcts(McstIterations, _newValueNetwork, _newPolicyNetwork);

        flowLayoutPanel1.BackColor = Color.Black;
        flowLayoutPanel1.BorderStyle = BorderStyle.None;
        flowLayoutPanel1.ForeColor = Color.White;

        statusStrip1.BackColor = Color.Black;
        toolStripStatusLabel1.BackColor = Color.Black;
        toolStripStatusLabel1.ForeColor = Color.White;

        listBox1.BackColor = Color.Black;
        listBox1.ForeColor = Color.White;

        label1.ForeColor = Color.White;

        button4.Text = "Parallel Play";

        // In your Form1.cs constructor, after initializing tab pages:
        foreach (TabPage tabPage in tabControl1.TabPages)
        {
            tabPage.Paint += (s, e) =>
            {
                e.Graphics.Clear(Color.Black);
            };
        }

        InitializeRedPercentChart();
        BackColor = Color.Black;

        _telemetryHistory.LoadFromFile();
        winPercentChart.DeepLearnThreshold = DeepLearningThreshold;

        this.Resize += Form1_Resize;

    }

    private static void EndGame(
        Connect4Game connect4Game,
        Mcts mcts,
        ListBox listBox,
        PictureBox pictureBox)
    {
        mcts.SetWinnerTelemetryHistory(connect4Game.Winner);
        connect4Game.ResetGame();

        pictureBox.Invoke(pictureBox.Refresh);
        listBox.Invoke(listBox.Items.Clear);
    }

    private static int PlacePiece(
        Connect4Game connect4Game,
        int column,
        ListBox listBox,
        PictureBox pictureBox)
    {
        int winner = connect4Game.PlacePieceColumn(column);

        _ = listBox.Invoke(() => _ = listBox.Items.Add(connect4Game.GameBoard.StateToString()));
        pictureBox.Invoke(pictureBox.Refresh);

        return winner;
    }

    private static int PlacePieceClick(
        Connect4Game connect4Game,
        MouseEventArgs? clickEvent,
        ListBox listBox,
        PictureBox pictureBox)
    {
        int winner = connect4Game.PlacePieceClick(clickEvent, pictureBox);

        _ = listBox.Invoke(() => _ = listBox.Items.Add(connect4Game.GameBoard.StateToString()));
        pictureBox.Invoke(pictureBox.Refresh);

        return winner;
    }

    private bool _isBattleArenaRunning = false;
    private CancellationTokenSource _arenaCancelationToken;

    private void Arena_Click(object sender, EventArgs e)
    {
        if (_isBattleArenaRunning)
        {
            _arenaCancelationToken.Cancel();
            _isBattleArenaRunning = false;
        }
        else
        {
            _arenaCancelationToken = new CancellationTokenSource();
            _isBattleArenaRunning = true;
            _ = Task.Run(BattleArena);
        }

    }

    private void ClearChart_Click(object sender, EventArgs e)
    {
        _redPercentHistory.Clear();
        _yellowPercentHistory.Clear();
        _drawPercentHistory.Clear();
        Invoke(() =>
        {
            winPercentChart.ClearData();
        });
        _ = MessageBox.Show("Chart history cleared successfully.");
    }

    private void Form1_Resize(object sender, EventArgs e)
    {
        winPercentChart.Invalidate();
    }

    private void InitializeRedPercentChart()
    {
        // Set up the chart
        winPercentChart.Title = "Win Rate History";
        winPercentChart.XAxisLabel = "Self-Play Session";
        winPercentChart.YAxisLabel = "Win Rate (%)";
        winPercentChart.YMin = 0;
        winPercentChart.YMax = 100;

        // Clear any existing data
        winPercentChart.ClearData();
    }

    private void LoadButton_Click(object sender, EventArgs e)
    {
        _telemetryHistory.LoadFromFile();

        _newValueNetwork = FlatDumbNetwork.CreateFromFile(OldValueNetwork) ?? _newValueNetwork;
        _newPolicyNetwork = FlatDumbNetwork.CreateFromFile(OldPolicyNetwork) ?? _newPolicyNetwork;
        _redMcts = new Mcts(McstIterations, _newValueNetwork, _newPolicyNetwork);

        _ = MessageBox.Show("telemetry and network loaded successfully.");
    }

    private void PictureBox1_Click(object? sender, EventArgs e)
    {
        var clickEvent = e as MouseEventArgs;
        int winner = PlacePieceClick(_connect4Game, clickEvent, listBox1, pictureBox1);

        if (winner != 0)
        {
            string color = winner == 1 ? "Red" : "Yellow";
            _ = MessageBox.Show($"{color} Player wins!");

            EndGame(_connect4Game, _redMcts, listBox1, pictureBox1);
        }

        int compMove = _redMcts.GetBestMove(_connect4Game.GameBoard, _connect4Game.CurrentPlayer == 1 ? 2 : 1)
            .GetAwaiter()
            .GetResult();

        winner = PlacePiece(_connect4Game, compMove, listBox1, pictureBox1);

        if (winner != 0)
        {
            string color = winner == 1 ? "Red" : "Yellow";
            _ = MessageBox.Show($"{color} Player wins!");

            EndGame(_connect4Game, _redMcts, listBox1, pictureBox1);
        }
    }

    private void PictureBox1_Paint(object? sender, PaintEventArgs e)
    {
        _connect4Game.DrawBoard(e.Graphics);
    }

    private void SaveButton_Click(object sender, EventArgs e)
    {
        _telemetryHistory.SaveToFile();

        _ = MessageBox.Show("Telemetry history saved successfully.");
    }

    private void SelfPlayButton_Click(object sender, EventArgs e)
    {
        if (_isParallelSelfPlayRunning)
        {
            _cancellationTokenSource?.Cancel();
            button4.Text = "Parallel Play";
            _isParallelSelfPlayRunning = false;
            toolStripStatusLabel1.Text = "Parallel self-play stopped";
        }
        else
        {
            button4.Text = "Stop";
            _isParallelSelfPlayRunning = true;
            toolStripStatusLabel1.Text = "Starting parallel self-play...";

            _ = Task.Run(() => SelfPlayParallelAsync(McstIterations));
        }
    }

    private void TrainButton_Click(object sender, EventArgs e)
    {
        //_ = Task.Run(() => TrainAsync(_redMcts, 0.05));
        //_ = Task.Run(() => TrainAsync(_yellowMcts, 0.05));

        TrainAsync(_redMcts, 0.05).GetAwaiter().GetResult();
        TrainAsync(_yellowMcts, 0.05).GetAwaiter().GetResult();
    }

    private void PictureBox2_Paint(object? sender, PaintEventArgs e)
    {
        _editorConnect4Game.DrawBoard(e.Graphics);
    }

    private void ReadBoardStateButton_Click(object sender, EventArgs e)
    {
        _editorConnect4Game.SetState(textBox1.Text);
        pictureBox2.Refresh();

    }
}