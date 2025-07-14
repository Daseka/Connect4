using Connect4.Ais;
using Connect4.GameParts;
using DeepNetwork;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Management;
using System.Runtime.InteropServices;

namespace Connect4;

public partial class Form1 : Form
{
    private const int ArenaIterations = 100;
    private const double MaximumError = 0.02;
    private const int MaxTrainingRuns = 5000;
    private const int McstIterations = 1600;
    private const string OldPolicyNetwork = "old_policy_network.json";
    private const string OldValueNetwork = "old_value_network.json";
    private const int SelfPlayGames = 100;
    private const int TrainingDataCount = 1050;
    private const int DeepLearningThreshold = 54;
    private readonly Connect4Game _connect4Game = new();

    // For parallel games
    private readonly List<GamePanel> _gamePanels = [];

    private SimpleDumbNetwork _newPolicyNetwork;
    private SimpleDumbNetwork _newValueNetwork;
    private readonly List<double> _redPercentHistory = new();
    private readonly List<double> _yellowPercentHistory = new();
    private readonly List<double> _drawPercentHistory = new();
    private readonly System.Timers.Timer _timer = new() { Interval = 100 };
    private CancellationTokenSource _cancellationTokenSource;
    private int _gamesPlayed = 0;
    private bool _isParallelSelfPlayRunning;
    private SimpleDumbNetwork _oldPolicyNetwork;
    private SimpleDumbNetwork _oldValueNetwork;
    private Mcts _redMcts;
    private double _redPercent;
    private Mcts _yellowMcts;
    private double _yellowPercent;
    private double _drawPercent;

    public Form1()
    {
        InitializeComponent();
        _timer.Elapsed += _timer_Elapsed;
        _timer.AutoReset = false;
        _timer.Start();

        pictureBox1.Size = new Size(650, 320);
        pictureBox1.Paint += PictureBox1_Paint;
        pictureBox1.Click += PictureBox1_Click;

        //_oldValueNetwork = new SimpleDumbNetwork([127, 67, 34, 1]);
        //_oldPolicyNetwork = new SimpleDumbNetwork([127, 67, 34, 7]);
        //_newValueNetwork = new SimpleDumbNetwork([127, 67, 34, 1]);
        //_newPolicyNetwork = new SimpleDumbNetwork([127, 67, 34, 7]);

        //_oldValueNetwork = new SimpleDumbNetwork([127, 137, 67, 1]);
        //_oldPolicyNetwork = new SimpleDumbNetwork([127, 137, 67, 7]);
        //_newValueNetwork = new SimpleDumbNetwork([127, 137, 67, 1]);
        //_newPolicyNetwork = new SimpleDumbNetwork([127, 137, 67, 7]);

        _oldValueNetwork = new SimpleDumbNetwork([127, 196, 98, 49, 1]);
        _oldPolicyNetwork = new SimpleDumbNetwork([127, 196, 98, 49, 7]);
        _newValueNetwork = new SimpleDumbNetwork([127, 196, 98, 49, 1]);
        _newPolicyNetwork = new SimpleDumbNetwork([127, 196, 98, 49, 7]);

        _oldValueNetwork = SimpleDumbNetwork.CreateFromFile(OldValueNetwork) ?? _oldValueNetwork;
        _oldPolicyNetwork = SimpleDumbNetwork.CreateFromFile(OldPolicyNetwork) ?? _oldPolicyNetwork;
        _oldPolicyNetwork.Trained = true;
        _oldValueNetwork.Trained = true;
        _yellowMcts = new Mcts(McstIterations, _oldValueNetwork, _oldPolicyNetwork);
        

        _newValueNetwork = SimpleDumbNetwork.CreateFromFile(OldValueNetwork) ?? _newValueNetwork;
        _newPolicyNetwork = SimpleDumbNetwork.CreateFromFile(OldPolicyNetwork) ?? _newPolicyNetwork;
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

        // Update button text for self-play
        button4.Text = "Parallel Play";

        // Initialize the chart
        InitializeRedPercentChart();
        BackColor = Color.Black;

        _redMcts.GetTelemetryHistory().LoadFromFile();
        
        winPercentChart.DeepLearnThreshold = DeepLearningThreshold;


        this.Resize += Form1_Resize;
    }

    private void Form1_Resize(object sender, EventArgs e)
    {
        winPercentChart.Invalidate();
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

    private async void _timer_Elapsed(object? sender, System.Timers.ElapsedEventArgs e)
    {
        if (checkBox1.Checked)
        {
            int compMove = await _redMcts.GetBestMove(_connect4Game.GameBoard, _connect4Game.CurrentPlayer == 1 ? 2 : 1);

            if (compMove == -1)
            {
                EndGame(_connect4Game, _redMcts, listBox1, pictureBox1);
                _ = Invoke(() => Text = $"Connect4 Games played {++_gamesPlayed}");
            }

            int winner = PlacePiece(_connect4Game, compMove, listBox1, pictureBox1);

            if (winner != 0)
            {
                EndGame(_connect4Game, _redMcts, listBox1, pictureBox1);
                _ = Invoke(() => Text = $"Connect4 Games played {++_gamesPlayed}");
            }
        }

        _timer.Start();
    }

    private void Arena_Click(object sender, EventArgs e)
    {
        _ = Task.Run(BattleArena);
    }

    private async Task BattleArena()
    {
        int bossLives = 3;
        bool skipTraining = false;
        for (int i = 0; i < ArenaIterations; i++)
        {
            await SelfPlayParallelAsync();

            if (_redPercent > DeepLearningThreshold)
            {

                if (bossLives <= 1)
                {
                    bossLives = 3;

                    //Only save the networks if Red has a win rate above deep learning threshold
                    _redMcts.ValueNetwork?.SaveToFile(OldValueNetwork);
                    _redMcts.PolicyNetwork?.SaveToFile(OldPolicyNetwork);

                    // Load the higher win rate networks for Yellow
                    _oldValueNetwork = SimpleDumbNetwork.CreateFromFile(OldValueNetwork) ?? _oldValueNetwork;
                    _oldPolicyNetwork = SimpleDumbNetwork.CreateFromFile(OldPolicyNetwork) ?? _oldPolicyNetwork;
                    _yellowMcts = new Mcts(McstIterations, _oldValueNetwork, _oldPolicyNetwork);

                    Invoke(() => toolStripStatusLabel1.Text = "Boss Dead: Yellow has new network");
                }
                else
                {
                    skipTraining = true;
                    bossLives--;
                    Invoke(() => toolStripStatusLabel1.Text = $"Boss Lives {bossLives}: Reduced boss life skipping training");
                }

            }
            else if(_redPercent < 30)
            {
                Invoke(() => toolStripStatusLabel1.Text = $"Boss Lives {bossLives}: boss unfased need more training");
            }
            else
            {
                Invoke(() => toolStripStatusLabel1.Text = $"Boss Lives {bossLives}: boss unfased need more training");
            }

            _redMcts.GetTelemetryHistory().SaveToFile();
            
            double maximumError = 0.50;
            if (!skipTraining)
            {
                await TrainAsync(_redMcts, maximumError);
            }

            skipTraining = false;
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
        _redMcts.GetTelemetryHistory().LoadFromFile();

        _newValueNetwork = SimpleDumbNetwork.CreateFromFile(OldValueNetwork) ?? _newValueNetwork;
        _newPolicyNetwork = SimpleDumbNetwork.CreateFromFile(OldPolicyNetwork) ?? _newPolicyNetwork;
        _redMcts = new Mcts(McstIterations, _newValueNetwork, _newPolicyNetwork);

        _ = MessageBox.Show("telemetry and network loaded successfully.");
    }

    private static void MergeTelemetry(Mcts source, Mcts target)
    {
        if (source != null && target != null)
        {
            TelemetryHistory sourceTelemetry = source.GetTelemetryHistory();
            TelemetryHistory targetTelemetry = target.GetTelemetryHistory();

            if (sourceTelemetry != null && targetTelemetry != null)
            {
                targetTelemetry.MergeFrom(sourceTelemetry);
            }
        }
    }

    private void PictureBox1_Click(object? sender, EventArgs e)
    {
        var clickEvent = e as MouseEventArgs;
        int winner = PlacePieceClick(_connect4Game, clickEvent, listBox1, pictureBox1);

        if (winner != 0)
        {
            string color = winner == 1 ? "Red" : "Yellow";
            _ = MessageBox.Show($"{color} Player wins!");

            EndGame(_connect4Game, _yellowMcts, listBox1, pictureBox1);
        }

        int compMove = _yellowMcts.GetBestMove(_connect4Game.GameBoard, _connect4Game.CurrentPlayer == 1 ? 2 : 1)
            .GetAwaiter()
            .GetResult();

        winner = PlacePiece(_connect4Game, compMove, listBox1, pictureBox1);

        if (winner != 0)
        {
            string color = winner == 1 ? "Red" : "Yellow";
            _ = MessageBox.Show($"{color} Player wins!");

            EndGame(_connect4Game, _yellowMcts, listBox1, pictureBox1);
        }
    }

    private void PictureBox1_Paint(object? sender, PaintEventArgs e)
    {
        _connect4Game.DrawBoard(e.Graphics);
    }

    private void SaveButton_Click(object sender, EventArgs e)
    {
        _redMcts.GetTelemetryHistory().SaveToFile();

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

            _ = Task.Run(SelfPlayParallelAsync);
        }
    }

    private async Task SelfPlayParallelAsync()
    {
        _cancellationTokenSource = new CancellationTokenSource();
        CancellationToken cancellationToken = _cancellationTokenSource.Token;

        int processorCount = Environment.ProcessorCount;
        int parallelGames = Math.Max(2, processorCount - 1);
        parallelGames = 16;

        int gamesPerThread = SelfPlayGames / parallelGames;
        int remainder = SelfPlayGames % parallelGames;

        Invoke(() =>
        {
            flowLayoutPanel1.Controls.Clear();
            _gamePanels.Clear();
        });

        // Create game panels for each thread game
        for (int i = 0; i < parallelGames; i++)
        {
            var gamePanel = new GamePanel(i + 1);
            Invoke(() =>
            {
                flowLayoutPanel1.Controls.Add(gamePanel);
                _gamePanels.Add(gamePanel);
            });
        }

        var sharedTelemetryHistory = new TelemetryHistory();
        var tasks = new List<Task>();
        var globalStats = new ConcurrentDictionary<int, (int Red, int Yellow, int Draw, int Total)>();

        for (int gameIndex = 0; gameIndex < parallelGames; gameIndex++)
        {
            int index = gameIndex;
            globalStats[index] = (0, 0, 0, 0);

            // Calculate how many games this thread should play
            // First 'remainingGames' threads get one extra game
            int gamesToPlay = gamesPerThread + (index < remainder ? 1 : 0);

            tasks.Add(Task.Run(async () =>
            {
                try
                {
                    var redMcts = new Mcts(McstIterations, _newValueNetwork, _newPolicyNetwork);
                    var yellowMcts = new Mcts(McstIterations, _oldValueNetwork, _oldPolicyNetwork);

                    int gamesPlayed = 0;
                    int redWins = 0;
                    int yellowWins = 0;
                    int draws = 0;

                    GamePanel panel = _gamePanels[index];
                    CompactConnect4Game game = panel.Game;
                    PictureBox pictureBox = panel.PictureBox;

                    while (!cancellationToken.IsCancellationRequested && gamesPlayed < gamesToPlay)
                    {
                        bool gameEnded = false;

                        while (!gameEnded && !cancellationToken.IsCancellationRequested)
                        {
                            Mcts mcts = game.CurrentPlayer == (int)Player.Red
                                ? redMcts
                                : yellowMcts;

                            int move = await mcts.GetBestMove(game.GameBoard, (int)game.GameBoard.LastPlayed);

                            if (move == -1)
                            {
                                mcts.SetWinnerTelemetryHistory(Winner.Draw);
                                game.ResetGame();
                                gameEnded = true;

                                draws++;
                                gamesPlayed++;

                                _ = BeginInvoke(() =>
                                {
                                    panel.RecordResult(Winner.Draw);
                                    pictureBox.Refresh();
                                });

                                globalStats[index] = (redWins, yellowWins, draws, gamesPlayed);
                                //if (gamesPlayed % 10 == 0)
                                //{
                                //    _ = BeginInvoke(() => UpdateGlobalStats(globalStats));
                                //}
                                _ = BeginInvoke(() => UpdateGlobalStats(globalStats));

                                continue;
                            }

                            int winner = game.PlacePieceColumn(move);
                            _ = BeginInvoke(() => pictureBox.Refresh());

                            if (winner != 0)
                            {
                                var winnerEnum = (Winner)winner;
                                mcts.SetWinnerTelemetryHistory(winnerEnum);
                                gameEnded = true;

                                if (winner == 1)
                                {
                                    redWins++;
                                }
                                else
                                {
                                    yellowWins++;
                                }

                                gamesPlayed++;

                                _ = BeginInvoke(() => panel.RecordResult(winnerEnum));

                                globalStats[index] = (redWins, yellowWins, draws, gamesPlayed);
                                //if (gamesPlayed % 10 == 0)
                                //{
                                //    _ = BeginInvoke(() => UpdateGlobalStats(globalStats));
                                //}

                                _ = BeginInvoke(() => UpdateGlobalStats(globalStats));

                                game.ResetGame();
                                _ = BeginInvoke(() => pictureBox.Refresh());

                                continue;
                            }
                        }
                    }

                    lock (sharedTelemetryHistory)
                    {
                        MergeTelemetry(redMcts, _redMcts);
                    }
                }
                catch (Exception ex)
                {
                    _ = BeginInvoke(() =>
                    {
                        toolStripStatusLabel1.Text = $"Error in game {index}: {ex.Message}";
                    });
                }
            }));
        }

        await Task.WhenAll(tasks).ConfigureAwait(false);

        _ = BeginInvoke(() => UpdateGlobalStats(globalStats));
        _redPercentHistory.Add(_redPercent);
        _yellowPercentHistory.Add(_yellowPercent);
        _drawPercentHistory.Add(_drawPercent);

        UpdatePercentChart();

        _ = BeginInvoke(() =>
        {
            if (!cancellationToken.IsCancellationRequested)
            {
                toolStripStatusLabel1.Text = "All parallel games completed!";
                button4.Text = "Parallel Play";
                _isParallelSelfPlayRunning = false;
            }
        });
    }

    private Task TrainAsync(Mcts mcts, double maximumError = MaximumError)
    {
        Invoke(() => listBox1.Items.Clear());

        mcts.GetTelemetryHistory().LoadFromFile();

        
        TelemetryHistory telemetryHistory = mcts.GetTelemetryHistory();
        int timesToTrain = Math.Min(5,telemetryHistory.Count / TrainingDataCount);

        for (int t = 0; t < timesToTrain; t++)
        {
            //-----------------------
            //Train the value network
            (double[][] valueTrainingData, double[][] valueExpectedData) = telemetryHistory
                .GetTrainingValueData(TrainingDataCount);

            int run = 0;
            int sameCount = 0;
            double previousError = 0;
            double error;

            var valueTrainer = new NetworkTrainer(mcts.ValueNetwork);
            var stopwatch = Stopwatch.StartNew();
            do
            {
                error = valueTrainer.Train(valueTrainingData, valueExpectedData);

                sameCount = previousError.Equals(error) ? sameCount + 1 : 0;
                previousError = error;
                run++;
                Invoke(() =>
                {
                    _ = listBox1.Items.Add($"Error {Math.Round(error, 5):F5} Runs {MaxTrainingRuns - run}");
                    listBox1.TopIndex = listBox1.Items.Count - 1;
                });
            }
            while (run < MaxTrainingRuns && error > maximumError && sameCount < 50);
            stopwatch.Stop();

            string x = $"Training value completed in {stopwatch.ElapsedMilliseconds} ms after {run} runs with error {error}";
            _ = Invoke(() => listBox1.Items.Add(x));

            //Test the network 10 times
            for (int i = 0; i < 10; i++)
            {
                int index = i;
                double[] input = valueTrainingData[index];
                double[] output = mcts.ValueNetwork.Calculate(input);

                //calculate the error
                double expected = valueExpectedData[index][0];
                double diffrence = Math.Abs(expected - output[0]);
                Invoke(() =>
                {
                    _ = listBox1.Items.Add($"Run {i:D2}: Difference {Math.Round(diffrence, 2):F2}");
                    listBox1.TopIndex = listBox1.Items.Count - 1;
                });
            }

            //-----------------------
            //Train the policy network
            (double[][] policyTrainingData, double[][] policyExpectedData) = telemetryHistory
                .GetTrainingPolicyData(TrainingDataCount);

            int run2 = 0;
            double sameCount2 = 0;
            double previousError2 = 0;
            double error2 = 0.0;

            var policyTrainer = new NetworkTrainer(mcts.PolicyNetwork);
            var stopwatch2 = Stopwatch.StartNew();
            do
            {
                error2 = policyTrainer.Train(policyTrainingData, policyExpectedData);

                sameCount2 = previousError2.Equals(error2) ? sameCount2 + 1 : 0;
                previousError2 = error2;
                run2++;
                Invoke(() =>
                {
                    _ = listBox1.Items.Add($"Error {Math.Round(error2, 5):F5} Runs {MaxTrainingRuns - run2}");
                    listBox1.TopIndex = listBox1.Items.Count - 1;
                });
            }
            while (run2 < MaxTrainingRuns && error2 > maximumError && sameCount2 < 50);
            stopwatch2.Stop();

            string text = $"Training profile completed in {stopwatch2.ElapsedMilliseconds} ms after {run2} runs with error {error2}";
            _ = Invoke(() => listBox1.Items.Add(text));

            //Test the network 10 times
            for (int i = 0; i < 10; i++)
            {
                int index = i;
                double[] input = policyTrainingData[index];
                double[] output = mcts.PolicyNetwork.Calculate(input);

                //calculate the error
                double expected = policyExpectedData[index][0];
                double diffrence = Math.Abs(expected - output[0]);
                Invoke(() =>
                {
                    _ = listBox1.Items.Add($"Run {i:D2}: Difference {Math.Round(diffrence, 2):F2}");
                    listBox1.TopIndex = listBox1.Items.Count - 1;
                });
            }
        }

        return Task.CompletedTask;
    }

    private void TrainButton_Click(object sender, EventArgs e)
    {
        //_ = Task.Run(() => TrainAsync(_redMcts, 0.05));
        //_ = Task.Run(() => TrainAsync(_yellowMcts, 0.05));

        TrainAsync(_redMcts, 0.05).GetAwaiter().GetResult();
        TrainAsync(_yellowMcts, 0.05).GetAwaiter().GetResult();
    }

    private void UpdateGlobalStats(ConcurrentDictionary<int, (int red, int yellow, int draw, int total)> globalStats)
    {
        int totalRed = 0;
        int totalYellow = 0;
        int totalDraw = 0;
        int totalGames = 0;

        foreach ((int red, int yellow, int draw, int total) in globalStats.Values)
        {
            totalRed += red;
            totalYellow += yellow;
            totalDraw += draw;
            totalGames += total;
        }

        _ = BeginInvoke(() =>
        {
            if (totalGames > 0)
            {
                _redPercent = Math.Round(totalRed / (double)totalGames * 100, 2);
                _yellowPercent = Math.Round(totalYellow / (double)totalGames * 100, 2);
                _drawPercent = Math.Round(totalDraw / (double)totalGames * 100, 2);

                Text = $"Progress: {totalGames}/{SelfPlayGames} - " +
                    $"Red: {_redPercent}% " +
                    $"Yellow: {_yellowPercent}% " +
                    $"Draw: {_drawPercent}%";

                int progressPercent = (int)(totalGames / (double)SelfPlayGames * 100);
                toolStripStatusLabel1.Text = $"Running: {totalGames}/{SelfPlayGames} games completed ({progressPercent}%)";
            }
        });
    }

    private void UpdatePercentChart()
    {
        if (_redPercentHistory.Count == 0)
        {
            return;
        }

        Invoke(() =>
        {
            winPercentChart.ClearData();

            for (int i = 0; i < _redPercentHistory.Count; i++)
            {
                winPercentChart.AddDataPoint(_redPercentHistory[i], _yellowPercentHistory[i], _drawPercentHistory[i]);
            }
        });
    }
}
