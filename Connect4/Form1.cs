using Connect4.Ais;
using Connect4.GameParts;
using DeepNetwork;
using DeepNetwork.NetworkIO;

namespace Connect4;

public partial class Form1 : Form
{
    private const int AgentCatalogSize = 10;

    private readonly Connect4Game _connect4Game = new();
    private readonly Connect4Game _editorConnect4Game = new();
    private readonly List<GamePanel> _gamePanels = [];

    private readonly int[] policyArray = [127, 512, 256, 128, 64, 7];
    private readonly int[] valueArray = [127, 512, 256, 128, 64, 1];
    private CancellationTokenSource _arenaCancelationSource = new();
    private CancellationTokenSource _coliseimCancelationSource = new();
    private int _gamesPlayed = 0;
    private bool _isBattleArenaRunning = false;
    private bool _isBattleColiseumRunning = false;
    private bool _isParallelSelfPlayRunning;
    private IStandardNetwork _newPolicyNetwork;
    private IStandardNetwork _newValueNetwork;
    private IStandardNetwork _oldPolicyNetwork;
    private IStandardNetwork _oldValueNetwork;
    private Mcts _redMcts;
    private Mcts _yellowMcts;

    //private readonly int[] valueArray = [127, 256, 128, 64, 32, 1];
    //private readonly int[] policyArray = [127, 256, 128, 64, 32, 7];
    public Form1()
    {
        InitializeComponent();

        //Test();

        pictureBox1.Size = new Size(650, 320);
        pictureBox1.Paint += PictureBox1_Paint;
        pictureBox1.Click += PictureBox1_Click;

        // Set up the second picture box for board editor
        pictureBox2.Size = new Size(650, 320);
        pictureBox2.Paint += PictureBox2_Paint;

        bool useMiniBatchNetwork = true;
        if (useMiniBatchNetwork)
        {
            _oldValueNetwork = new MiniBatchMatrixNetwork(valueArray, isSoftmax: false);
            _oldPolicyNetwork = new MiniBatchMatrixNetwork(policyArray, isSoftmax: true);
            _newValueNetwork = new MiniBatchMatrixNetwork(valueArray, isSoftmax: false);
            _newPolicyNetwork = new MiniBatchMatrixNetwork(policyArray, isSoftmax: true);
        }
        else
        {
            _oldValueNetwork = new FlatDumbNetwork([127, 256, 128, 64, 3]);
            _oldPolicyNetwork = new FlatDumbNetwork([127, 256, 128, 64, 7]);
            _newValueNetwork = new FlatDumbNetwork([127, 256, 128, 64, 3]);
            _newPolicyNetwork = new FlatDumbNetwork([127, 256, 128, 64, 7]);
        }

        _agentCatalog = new AgentCatalog(AgentCatalogSize);
        _agentCatalog.LoadCatalog();
        _currentAgent = _agentCatalog.GetLatestAgents(1).FirstOrDefault();

        IStandardNetwork valueNetwork = _currentAgent?.ValueNetwork ?? _oldValueNetwork;
        valueNetwork.Trained = true;
        IStandardNetwork policyNetwork = _currentAgent?.PolicyNetwork ?? _oldPolicyNetwork;
        policyNetwork.Trained = true;

        _yellowMcts = new Mcts(McstIterations, valueNetwork.Clone(), policyNetwork.Clone());
        _redMcts = new Mcts(McstIterations, valueNetwork.Clone(), policyNetwork.Clone());

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

        _trainingBuffer.LoadFromFile();
        winPercentChart.DeepLearnThreshold = DeepLearningThreshold;

        Resize += Form1_Resize;

        var singleTabControl = new SingleGameControl(_agentCatalog) { Dock = DockStyle.Fill };
        tabPage3.Controls.Clear();
        tabPage3.Controls.Add(singleTabControl);

        // Add BoardStateReaderControl to tabPage4
        var boardStateReaderControl = new BoardStateReaderControl(_agentCatalog) { Dock = DockStyle.Fill };
        tabPage4.Controls.Clear();
        tabPage4.Controls.Add(boardStateReaderControl);
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

    private void Arena_Click(object sender, EventArgs e)
    {
        try
        {
            if (_isBattleArenaRunning)
            {
                _arenaCancelationSource.Cancel();
                _isBattleArenaRunning = false;
                button5.Text = "Arena";
            }
            else
            {
                _arenaCancelationSource = new CancellationTokenSource();
                _isBattleArenaRunning = true;
                button5.Text = "Stop Arena";
                _ = Task.Run(BattleArena);
            }
        }
        catch (Exception ex)
        {
            _ = MessageBox.Show($"Error starting/stopping arena: {ex.Message} \n{ex.StackTrace}");
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

    private void Coliseum_Click(object sender, EventArgs e)
    {
        if (_isBattleColiseumRunning)
        {
            _coliseimCancelationSource.Cancel();
            _isBattleColiseumRunning = false;
            button2.Text = "Coliseum";
        }
        else
        {
            _coliseimCancelationSource = new CancellationTokenSource();
            _isBattleColiseumRunning = true;
            button2.Text = "Stop Coliseum";
            _ = Task.Run(BattleColiseum);
        }
    }

    private void EndGame(
                            Connect4Game connect4Game,
        Mcts mcts,
        ListBox listBox,
        PictureBox pictureBox)
    {
        mcts.SetWinnerTrainingBuffer(connect4Game.Winner);
        connect4Game.ResetGame();
        _trainingBuffer.MergeFrom(mcts.GetTrainingBuffer());

        pictureBox.Invoke(pictureBox.Refresh);
        listBox.Invoke(listBox.Items.Clear);
    }

    private void Form1_FormClosing(object sender, FormClosingEventArgs e)
    {
        var save = MessageBox.Show(
            "Save Training buffer & Arena catalog?",
            "Confirm Save",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Question) == DialogResult.Yes;

        if (save)
        {
            _trainingBuffer.SaveToFile();
            _replayBuffer.SaveToFile();
            _agentCatalog.SaveCatalog();
        }
    }

    private void Form1_Resize(object? sender, EventArgs e)
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

    private void PictureBox1_Click(object? sender, EventArgs e)
    {
        var clickEvent = e as MouseEventArgs;
        int winner = PlacePieceClick(_connect4Game, clickEvent, listBox1, pictureBox1);

        if (winner != 0)
        {
            string color = winner == 1 ? "Red" : "Yellow";
            _ = MessageBox.Show($"{color} Player wins!");

            EndGame(_connect4Game, _redMcts, listBox1, pictureBox1);
            return;
        }

        int compMove = _redMcts.GetBestMove(
            _connect4Game.GameBoard,
            _connect4Game.CurrentPlayer == 1 ? 2 : 1,
            ExplorationConstant,
            0,
            true)
            .GetAwaiter()
            .GetResult();

        winner = PlacePiece(_connect4Game, compMove, listBox1, pictureBox1);

        if (winner != 0)
        {
            string color = winner == 1 ? "Red" : "Yellow";
            _ = MessageBox.Show($"{color} Player wins!");

            EndGame(_connect4Game, _redMcts, listBox1, pictureBox1);
        }

        if (_connect4Game.GameBoard.HasDraw())
        {
            _ = MessageBox.Show("It's a draw!");

            EndGame(_connect4Game, _redMcts, listBox1, pictureBox1);
        }
    }

    private void PictureBox1_Paint(object? sender, PaintEventArgs e)
    {
        _connect4Game.DrawBoard(e.Graphics);
    }

    private void PictureBox2_Paint(object? sender, PaintEventArgs e)
    {
        _editorConnect4Game.DrawBoard(e.Graphics);
    }

    private void ReadBoardStateButton_Click(object sender, EventArgs e)
    {
        _editorConnect4Game.SetState(textBox1.Text);
        pictureBox2.Refresh();

        Text = $"LastPlayed {_editorConnect4Game.GameBoard.LastPlayed}";
    }

    private void ResetButton_Click(object sender, EventArgs e)
    {
        _connect4Game.ResetGame();
        pictureBox1.Refresh();
    }

    private void SelfPlayButton_Click(object sender, EventArgs e)
    {
        if (_isParallelSelfPlayRunning)
        {
            _arenaCancelationSource?.Cancel();
            button4.Text = "Parallel Play";
            _isParallelSelfPlayRunning = false;
            toolStripStatusLabel1.Text = "Parallel self-play stopped";
        }
        else
        {
            button4.Text = "Stop";
            _isParallelSelfPlayRunning = true;
            toolStripStatusLabel1.Text = "Starting parallel self-play...";

            _ = Task.Run(() => VsPlayParallel(_redMcts, _yellowMcts, McstIterations, explorationFactor: ExplorationConstant));
        }
    }

    private void Test()
    {
        _trainingBuffer.LoadFromFile();

        _oldValueNetwork = new MiniBatchMatrixNetwork(valueArray, isSoftmax: false);
        _oldPolicyNetwork = new MiniBatchMatrixNetwork(policyArray, isSoftmax: true);

        INetworkTrainer valueTrainer = NetworkTrainerFactory.CreateNetworkTrainer(_oldValueNetwork);
        INetworkTrainer policyTrainer = NetworkTrainerFactory.CreateNetworkTrainer(_oldPolicyNetwork);

        (double[][] input, double[][] policyOutput, double[][] valueOutput) trainingData = _trainingBuffer.GetTrainingDataRandom(1000);

        double[][] inputTrain = [.. trainingData.input.Skip(100)];
        double[][] inputTest = [.. trainingData.input.Take(100)];
        double[][] policyTrain = [.. trainingData.policyOutput.Skip(100)];
        double[][] policyTest = [.. trainingData.policyOutput.Take(100)];
        double[][] valueTrain = [.. trainingData.valueOutput.Skip(100)];
        double[][] valueTest = [.. trainingData.valueOutput.Take(100)];

        var results = new List<string>();
        for (int i = 0; i < inputTest.Length; i++)
        {
            double[] valueResult = _oldValueNetwork.Calculate(inputTest[i]);
            double[] policyResult = _oldPolicyNetwork.Calculate(inputTest[i]);

            results.Add($"Value out: {string.Join(", ", valueResult.Select(x => x.ToString("F3")))}");
            results.Add($"Value org: {string.Join(", ", valueTest[i].Select(x => x.ToString("F3")))}");
            results.Add($"Policy out: {string.Join(", ", policyResult.Select(x => x.ToString("F3")))}");
            results.Add($"Policy org: {string.Join(", ", policyTest[i].Select(x => x.ToString("F3")))}");
        }

        results = new List<string>();
        for (int i = 0; i < 200; i++)
        {
            valueTrainer.Train(inputTrain, valueTrain);
            policyTrainer.Train(inputTrain, policyTrain);
            //valueTrainer.Train(inputTest, valueTest);
            //policyTrainer.Train(inputTest, policyTest);
        }

        for (int i = 0; i < inputTest.Length; i++)
        {
            double[] valueResult = _oldValueNetwork.Calculate(inputTest[i]);
            double[] policyResult = _oldPolicyNetwork.Calculate(inputTest[i]);

            results.Add($"Value out: {string.Join(", ", valueResult.Select(x => x.ToString("F3")))}");
            results.Add($"Value org: {string.Join(", ", valueTest[i].Select(x => x.ToString("F3")))}");
            results.Add($"Policy out: {string.Join(", ", policyResult.Select(x => x.ToString("F3")))}");
            results.Add($"Policy org: {string.Join(", ", policyTest[i].Select(x => x.ToString("F3")))}");
        }
    }
}