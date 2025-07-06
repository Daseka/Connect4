using Connect4.Ais;
using Connect4.GameParts;
using DeepNetwork;
using System.Collections.Concurrent;
using System.Diagnostics;

namespace Connect4;

public partial class Form1 : Form
{
    private const double MaximumError = 0.01;
    private const int MaxTrainingRuns = 5000;
    private const int McstIterations = 800;
    private const string OldPolicyNetwork = "old_policy_network.json";
    private const string OldValueNetwork = "old_value_network.json";
    private const int SelfPlayGames = 100;
    private const int ArenaIterations = 100;
    private readonly Connect4Game _connect4Game = new();
    private readonly SimpleDumbNetwork _newPolicyNetwork;
    private readonly SimpleDumbNetwork _newValueNetwork;
    private readonly System.Timers.Timer _timer = new() { Interval = 100 };
    private CancellationTokenSource _cancellationTokenSource;
    // For parallel games
    private List<GamePanel> _gamePanels = new();

    private int _gamesPlayed = 0;
    private bool _isParallelSelfPlayRunning;
    private SimpleDumbNetwork _oldPolicyNetwork;
    private SimpleDumbNetwork _oldValueNetwork;
    private Mcts _redMcts;
    private int _TimesLeftWithRandomYellow;
    private Mcts _yellowMcts;
    public Form1()
    {
        InitializeComponent();

        _timer.Elapsed += _timer_Elapsed;
        _timer.AutoReset = false;
        _timer.Start();

        pictureBox1.Size = new Size(650, 320);
        pictureBox1.Paint += PictureBox1_Paint;
        pictureBox1.Click += PictureBox1_Click;

        _oldValueNetwork = new SimpleDumbNetwork([127, 137, 67, 1]);
        _oldPolicyNetwork = new SimpleDumbNetwork([127, 137, 67, 7]);
        _newValueNetwork = new SimpleDumbNetwork([127, 95, 38, 1]);
        _newPolicyNetwork = new SimpleDumbNetwork([127, 124, 32, 7]);

        _oldValueNetwork = SimpleDumbNetwork.CreateFromFile(OldValueNetwork) ?? _oldValueNetwork;
        _oldPolicyNetwork = SimpleDumbNetwork.CreateFromFile(OldPolicyNetwork) ?? _oldPolicyNetwork;
        _yellowMcts = new Mcts(McstIterations, _oldValueNetwork, _oldPolicyNetwork);

        _newValueNetwork = SimpleDumbNetwork.CreateFromFile(OldValueNetwork) ?? _newValueNetwork;
        _newPolicyNetwork = SimpleDumbNetwork.CreateFromFile(OldPolicyNetwork) ?? _newPolicyNetwork;
        _redMcts = new Mcts(McstIterations, _newValueNetwork, _newPolicyNetwork);

        _TimesLeftWithRandomYellow = 3;

        // Update button text for self-play
        button4.Text = "Parallel Play";
    }

    private static void DisplayStats(int drawCount, int redWinCount, int yellowWinCount, int gameCount, Form form)
    {
        _ = form.Invoke(() => form.Text = $"Games played {gameCount} " +
        $"Red: {Math.Round(redWinCount / (double)gameCount * 100, 2)}% " +
        $"Yellow: {Math.Round(yellowWinCount / (double)gameCount * 100, 2)}% " +
        $"Draw: {Math.Round(drawCount / (double)gameCount * 100, 2)}");
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

    private static async Task SelfPlayAsync(
        Mcts RedMcts,
        Mcts YellowMcts,
        Connect4Game game,
        ListBox listBox,
        PictureBox pictureBox,
        Form form)
    {
        int count = 0;
        int drawCount = 0;
        int redWinCount = 0;
        int yellowWinCount = 0;

        TelemetryHistory telemetryHistory = RedMcts.GetTelemetryHistory();
        bool ended = false;

        while (telemetryHistory.Count < SelfPlayGames || !ended)
        {
            ended = false;
            Mcts mcts = game.CurrentPlayer == (int)Player.Red
                ? RedMcts
                : YellowMcts;

            int move = await mcts.GetBestMove(game.GameBoard, (int)game.GameBoard.LastPlayed);
            if (move == -1)
            {
                EndGame(game, mcts, listBox, pictureBox);
                ended = true;

                drawCount++;
                count++;

                DisplayStats(drawCount, redWinCount, yellowWinCount, count, form);

                continue;
            }

            int winner = PlacePiece(game, move, listBox, pictureBox);
            if (winner != 0)
            {
                EndGame(game, mcts, listBox, pictureBox);
                ended |= true;

                if (winner == 1)
                {
                    redWinCount++;
                }
                else if (winner == 2)
                {
                    yellowWinCount++;
                }

                count++;
                DisplayStats(drawCount, redWinCount, yellowWinCount, count, form);

                continue;
            }
        }

        DisplayStats(drawCount, redWinCount, yellowWinCount, count, form);
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
        Task.Run(() => BattleArena());
    }

    private async Task BattleArena()
    {
        for (int i = 0; i < ArenaIterations; i++)
        {
            // Use SelfPlayParallelAsync instead of SelfPlayAsync for improved performance
            await SelfPlayParallelAsync();

            if (_TimesLeftWithRandomYellow > 0)
            {
                _TimesLeftWithRandomYellow--;
            }
            else
            {
                _oldValueNetwork = SimpleDumbNetwork.CreateFromFile(OldValueNetwork) ?? _oldValueNetwork;
                _oldPolicyNetwork = SimpleDumbNetwork.CreateFromFile(OldPolicyNetwork) ?? _oldPolicyNetwork;
                _yellowMcts = new Mcts(McstIterations, _oldValueNetwork, _oldPolicyNetwork);
            }

            _redMcts.GetTelemetryHistory().SaveToFile();

            await TrainAsync(_redMcts);

            _redMcts.ValueNetwork?.SaveToFile(OldValueNetwork);
            _redMcts.PolicyNetwork?.SaveToFile(OldPolicyNetwork);

            TelemetryHistory telemetryHistory = _redMcts.GetTelemetryHistory();
            telemetryHistory.SaveToFile();
        }
    }

    private async Task SelfPlayParallelAsync()
    {
        try
        {
            _cancellationTokenSource = new CancellationTokenSource();
            CancellationToken cancellationToken = _cancellationTokenSource.Token;

            int processorCount = Environment.ProcessorCount;
            int parallelGames = Math.Max(2, processorCount - 1);

            // Calculate games per thread and the remaining games
            int baseGamesPerThread = SelfPlayGames / parallelGames;
            int remainingGames = SelfPlayGames % parallelGames;

            Invoke(() =>
            {
                toolStripStatusLabel1.Text = $"Running {parallelGames} parallel games...";
                flowLayoutPanel1.Controls.Clear();
                _gamePanels.Clear();
            });

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
                int gamesToPlay = baseGamesPerThread + (index < remainingGames ? 1 : 0);

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

                                    Invoke(() =>
                                    {
                                        panel.RecordResult(Winner.Draw);
                                        pictureBox.Refresh();
                                    });

                                    globalStats[index] = (redWins, yellowWins, draws, gamesPlayed);
                                    UpdateGlobalStats(globalStats);

                                    continue;
                                }

                                int winner = game.PlacePieceColumn(move);

                                Invoke(() =>
                                {
                                    pictureBox.Refresh();
                                });

                                await Task.Delay(10, cancellationToken);

                                if (winner != 0)
                                {
                                    Winner winnerEnum = (Winner)winner;
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

                                    Invoke(() =>
                                    {
                                        panel.RecordResult(winnerEnum);
                                    });

                                    globalStats[index] = (redWins, yellowWins, draws, gamesPlayed);
                                    UpdateGlobalStats(globalStats);

                                    game.ResetGame();
                                    Invoke(() => pictureBox.Refresh());

                                    continue;
                                }
                            }
                        }

                        lock (sharedTelemetryHistory)
                        {
                            MergeTelemetry(redMcts, _redMcts);
                        }

                    }
                    catch (OperationCanceledException)
                    {
                        // Normal cancellation, do nothing
                    }
                    catch (Exception ex)
                    {
                        Invoke(() =>
                        {
                            toolStripStatusLabel1.Text = $"Error in game {index}: {ex.Message}";
                        });
                    }
                }));
                //}, cancellationToken));
            }

            try
            {
                // Wait for all games to complete
                await Task.Run(() => Task.WhenAll(tasks), cancellationToken);

                await Task.WhenAll(tasks);

                // Save telemetry when all games are done
                _redMcts.GetTelemetryHistory().SaveToFile();

                Invoke(() =>
                {
                    if (!cancellationToken.IsCancellationRequested)
                    {
                        toolStripStatusLabel1.Text = "All parallel games completed!";
                        button4.Text = "Parallel Play";
                        _isParallelSelfPlayRunning = false;
                    }
                });
            }
            catch (OperationCanceledException)
            {
                // Handling cancellation
                Invoke(() =>
                {
                    toolStripStatusLabel1.Text = "Parallel self-play cancelled";
                });
            }
        }
        catch (Exception ex)
        {
            Invoke(() =>
            {
                toolStripStatusLabel1.Text = $"Error in parallel self-play: {ex.Message}";
                button4.Text = "Parallel Play";
                _isParallelSelfPlayRunning = false;
            });
        }
    }

    private void LoadButton_Click(object sender, EventArgs e)
    {
        _redMcts.GetTelemetryHistory().LoadFromFile();

        _oldValueNetwork = SimpleDumbNetwork.CreateFromFile(OldValueNetwork) ?? _oldPolicyNetwork;
        _oldPolicyNetwork = SimpleDumbNetwork.CreateFromFile(OldPolicyNetwork) ?? _oldPolicyNetwork;
        _redMcts = new Mcts(McstIterations, _oldValueNetwork, _oldPolicyNetwork);

        _ = MessageBox.Show("telemetry and network loaded successfully.");
    }

    private void MergeTelemetry(Mcts source, Mcts target)
    {
        // Use the new MergeFrom method to merge telemetry from source to target
        if (source != null && target != null)
        {
            var sourceTelemetry = source.GetTelemetryHistory();
            var targetTelemetry = target.GetTelemetryHistory();

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
            // Cancel ongoing parallel self-play
            _cancellationTokenSource?.Cancel();
            button4.Text = "Parallel Play";
            _isParallelSelfPlayRunning = false;
            toolStripStatusLabel1.Text = "Parallel self-play stopped";
        }
        else
        {
            // Start parallel self-play
            button4.Text = "Stop";
            _isParallelSelfPlayRunning = true;
            toolStripStatusLabel1.Text = "Starting parallel self-play...";

            // Run in background to keep UI responsive
            Task.Run(() => SelfPlayParallelAsync());
        }
    }

    private Task TrainAsync(Mcts mcts)
    {
        mcts.GetTelemetryHistory().LoadFromFile();
        //-----------------------
        //Train the value network
        (double[][] valueTrainingData, double[][] valueExpectedData) = mcts
            .GetTelemetryHistory()
            .GetTrainingValueData(2000);

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
                _ = listBox1.Items.Add($"Error {Math.Round(error, 3):F5}");
                listBox1.TopIndex = listBox1.Items.Count - 1;
            });
        }
        while (run < MaxTrainingRuns && error > MaximumError && sameCount < 50);
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
        (double[][] policyTrainingData, double[][] policyExpectedData) = mcts.GetTelemetryHistory().GetTrainingPolicyData();
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
                _ = listBox1.Items.Add($"Error {Math.Round(error2, 3):F5}");
                listBox1.TopIndex = listBox1.Items.Count - 1;
            });
        }
        while (run2 < MaxTrainingRuns && error2 > MaximumError && sameCount2 < 50);
        stopwatch2.Stop();

        var text = $"Training profile completed in {stopwatch2.ElapsedMilliseconds} ms after {run2} runs with error {error2}";
        _ = Invoke(() => listBox1.Items.Add(text));

        //Test the network 10 times
        for (int i = 0; i < 10; i++)
        {
            int index = i;
            double[] input = policyTrainingData[index];
            double[] output = mcts.PolicyNetwork.Calculate(input);

            //calculate the error
            double expected = policyExpectedData[index][0];
            var diffrence = Math.Abs(expected - output[0]);
            Invoke(() =>
            {
                listBox1.Items.Add($"Run {i:D2}: Difference {Math.Round(diffrence, 2):F2}");
                listBox1.TopIndex = listBox1.Items.Count - 1;
            });
        }

        return Task.CompletedTask;
    }

    private void TrainButton_Click(object sender, EventArgs e)
    {
        Task.Run(() => TrainAsync(_redMcts));
    }

    private void UpdateGlobalStats(ConcurrentDictionary<int, (int Red, int Yellow, int Draw, int Total)> globalStats)
    {
        int totalRed = 0;
        int totalYellow = 0;
        int totalDraw = 0;
        int totalGames = 0;

        foreach (var stats in globalStats.Values)
        {
            totalRed += stats.Red;
            totalYellow += stats.Yellow;
            totalDraw += stats.Draw;
            totalGames += stats.Total;
        }

        Invoke(() =>
        {
            if (totalGames > 0)
            {
                double redPercent = Math.Round(totalRed / (double)totalGames * 100, 2);
                double yellowPercent = Math.Round(totalYellow / (double)totalGames * 100, 2);
                double drawPercent = Math.Round(totalDraw / (double)totalGames * 100, 2);

                this.Text = $"Progress: {totalGames}/{SelfPlayGames} - " +
                    $"Red: {redPercent}% " +
                    $"Yellow: {yellowPercent}% " +
                    $"Draw: {drawPercent}%";

                // Also update the status strip to show progress
                int progressPercent = (int)(totalGames / (double)SelfPlayGames * 100);
                toolStripStatusLabel1.Text = $"Running: {totalGames}/{SelfPlayGames} games completed ({progressPercent}%)";
            }
        });
    }
}