using Connect4.Ais;
using Connect4.GameParts;
using DeepNetwork;
using System.Collections.Concurrent;
using System.Diagnostics;

namespace Connect4;

public partial class Form1 : Form
{
    private const int ArenaIterations = 100;
    private const int DeepLearningThreshold = 51;
    private const double MaximumError = 0.10;
    private const int MaxTrainingRuns = 2000;
    private const int McstIterations = 400;
    private const string OldPolicyNetwork = "telemetry\\old_policy_network.json";
    private const string OldValueNetwork = "telemetry\\old_value_network.json";
    private const int SelfPlayGames = 1000;
    private const int TelemetryHistorySaturation = 2100;
    private const int TrainingDataCount = 2100;

    private readonly List<double> _drawPercentHistory = [];
    private readonly List<double> _redPercentHistory = [];
    private readonly TelemetryHistory _telemetryHistory = new();
    private readonly List<double> _yellowPercentHistory = [];
    private double _drawPercent;
    private double _redPercent;
    private double _redWithDrawPercent;
    private double _yellowPercent;

    private async Task BattleArena()
    {
        int bossLives = 3;
        bool skipTraining = false;
        int lastPolicyTrainingRuns = 0;
        double lastPolicyTrainingError = double.MaxValue;
        double maximumError = 0.20;

        int i = 0;
        while (i < ArenaIterations && !_arenaCancelationToken.IsCancellationRequested)
        {
            i++;
            do
            {
                // keep playing until we have enough data
                await SelfPlayParallelAsync(McstIterations);
            }
            while (_telemetryHistory.Count < TelemetryHistorySaturation);

            _telemetryHistory.SaveToFile();

            if (_redWithDrawPercent > DeepLearningThreshold)
            {
                if (bossLives <= 1)
                {
                    bossLives = 3;

                    //Only save the networks if Red has a win rate above deep learning threshold
                    _redMcts.ValueNetwork?.SaveToFile(OldValueNetwork);
                    _redMcts.PolicyNetwork?.SaveToFile(OldPolicyNetwork);

                    // Load the higher win rate networks for Yellow
                    _oldValueNetwork = FlatDumbNetwork.CreateFromFile(OldValueNetwork) ?? _oldValueNetwork;
                    _oldPolicyNetwork = FlatDumbNetwork.CreateFromFile(OldPolicyNetwork) ?? _oldPolicyNetwork;
                    _yellowMcts = new Mcts(McstIterations, _oldValueNetwork, _oldPolicyNetwork);

                    BeginInvoke(() => toolStripStatusLabel1.Text = "Boss Dead: Yellow has new network");
                }
                else
                {
                    skipTraining = true;
                    bossLives--;
                    BeginInvoke(() => toolStripStatusLabel1.Text = $"Boss Lives {bossLives}: Reduced boss life skipping training");
                }
            }
            else
            {
                bossLives += bossLives < 3 ? 1 : 0;
                BeginInvoke(() => toolStripStatusLabel1.Text = $"Boss Lives {bossLives}: boss unfased need more training");
            }

            if (_arenaCancelationToken.IsCancellationRequested)
            {
                BeginInvoke(() => toolStripStatusLabel1.Text = "Battle Arena cancelled.");
                return;
            }

            if (lastPolicyTrainingRuns >= MaxTrainingRuns)
            {
                maximumError += 0.02;
            }
            else if (lastPolicyTrainingRuns < 100 && lastPolicyTrainingRuns > 0 && maximumError > MaximumError)
            {
                maximumError -= 0.02;
            }

            if (!skipTraining)
            {
                (lastPolicyTrainingRuns, lastPolicyTrainingError) = await TrainAsync(_redMcts, maximumError);
            }

            skipTraining = false;
        }
    }

    private async Task SelfPlayParallelAsync(int mcstIterations)
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
                    var redMcts = new Mcts(mcstIterations, _newValueNetwork, _newPolicyNetwork);
                    var yellowMcts = new Mcts(mcstIterations, _oldValueNetwork, _oldPolicyNetwork);

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
                                _ = BeginInvoke(() => UpdateGlobalStats(globalStats));

                                continue;
                            }

                            int winner = game.PlacePieceColumn(move);
                            _ = BeginInvoke(() => pictureBox.Refresh());

                            if (winner != 0)
                            {
                                mcts.SetWinnerTelemetryHistory(game.Winner);
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

                                _ = BeginInvoke(() => panel.RecordResult(game.Winner));
                                globalStats[index] = (redWins, yellowWins, draws, gamesPlayed);
                                _ = BeginInvoke(() => UpdateGlobalStats(globalStats));

                                game.ResetGame();
                                _ = BeginInvoke(() => pictureBox.Refresh());

                                continue;
                            }
                        }
                    }

                    lock (sharedTelemetryHistory)
                    {
                        _telemetryHistory.MergeFrom(redMcts.GetTelemetryHistory());
                        _telemetryHistory.MergeFrom(yellowMcts.GetTelemetryHistory());
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
        _redPercentHistory.Add(_redWithDrawPercent);
        _yellowPercentHistory.Add(_yellowPercent);
        _drawPercentHistory.Add(_drawPercent);

        UpdatePercentChart(DeepLearningThreshold);

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

    private Task<(int runs, double error)> TrainAsync(Mcts mcts, double maximumError = MaximumError)
    {
        Invoke(() => listBox1.Items.Clear());

        _telemetryHistory.LoadFromFile();

        TelemetryHistory telemetryHistory = _telemetryHistory;
        int timesToTrain = 1;
        var random = new Random();
        int minPolicRuns = int.MaxValue;
        double minPolicyError = double.MaxValue;
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

            var valueTrainer = new FlatNetworkTrainer(mcts.ValueNetwork);
            var stopwatch = Stopwatch.StartNew();
            do
            {
                error = valueTrainer.Train(valueTrainingData, valueExpectedData);

                sameCount = previousError.Equals(Math.Round(error, 8)) ? sameCount + 1 : 0;
                previousError = Math.Round(error, 8);
                run++;
                BeginInvoke(() =>
                {
                    _ = listBox1.Items.Add($"Error {Math.Round(error, 8):F8} Runs {MaxTrainingRuns - run}");
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
                int index = random.Next(0, valueTrainingData.Length);
                double[] input = valueTrainingData[index];
                double[] output = mcts.ValueNetwork.Calculate(input);

                //calculate the error
                double expected = valueExpectedData[index][0];
                double diffrence = Math.Abs(expected - output[0]);
                BeginInvoke(() =>
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

            var policyTrainer = new FlatNetworkTrainer(mcts.PolicyNetwork);
            var stopwatch2 = Stopwatch.StartNew();
            do
            {
                error2 = policyTrainer.Train(policyTrainingData, policyExpectedData);

                sameCount2 = previousError2.Equals(error2) ? sameCount2 + 1 : 0;
                previousError2 = error2;
                run2++;
                BeginInvoke(() =>
                {
                    _ = listBox1.Items.Add($"Error {Math.Round(error2, 8):F8} Runs {MaxTrainingRuns - run2}");
                    listBox1.TopIndex = listBox1.Items.Count - 1;
                });
            }
            while (run2 < MaxTrainingRuns && error2 > maximumError && sameCount2 < 50);
            stopwatch2.Stop();

            string text = $"Training profile completed in {stopwatch2.ElapsedMilliseconds} ms after {run2} runs with error {error2} same {sameCount2}";
            _ = BeginInvoke(() => listBox1.Items.Add(text));

            //Test the network 10 times
            for (int i = 0; i < 10; i++)
            {
                int index = random.Next(0, policyTrainingData.Length);
                double[] input = policyTrainingData[index];
                double[] output = mcts.PolicyNetwork.Calculate(input);

                //calculate the error
                double expected = policyExpectedData[index][0];
                double diffrence = Math.Abs(expected - output[0]);
                BeginInvoke(() =>
                {
                    _ = listBox1.Items.Add($"Run {i:D2}: Difference {Math.Round(diffrence, 2):F2}");
                    listBox1.TopIndex = listBox1.Items.Count - 1;
                });
            }

            minPolicRuns = minPolicRuns < run2 ? minPolicRuns : run2;
            minPolicyError = minPolicyError < error2 ? minPolicyError : error2;
        }

        string message = $"Last Max error = {maximumError} next Max error = {minPolicyError}";
        Invoke(() =>
        {
            listBox1.Items.Add(message);
            listBox1.TopIndex = listBox1.Items.Count - 1;
        });

        return Task.FromResult<(int, double)>((minPolicRuns, minPolicyError));
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
                _redWithDrawPercent = Math.Round(_redPercent + _drawPercent / 2, 2);

                Text = $"{totalGames}/{SelfPlayGames} - " +
                    $"R: {_redPercent:F2}% ({_redWithDrawPercent:F2}%)" +
                    $"Y: {_yellowPercent:F2}% " +
                    $"D: {_drawPercent:F2}%" +
                    $"Data: {_telemetryHistory.Count}";

                int progressPercent = (int)(totalGames / (double)SelfPlayGames * 100);
                toolStripStatusLabel1.Text = $"Running: {totalGames}/{SelfPlayGames} games completed ({progressPercent}%)";
            }
        });
    }

    private void UpdatePercentChart(int deepLearningThreshold)
    {
        if (_redPercentHistory.Count == 0)
        {
            return;
        }

        Invoke(() =>
        {
            winPercentChart.ClearData();
            winPercentChart.DeepLearnThreshold = deepLearningThreshold;

            for (int i = 0; i < _redPercentHistory.Count; i++)
            {
                winPercentChart.AddDataPoint(_redPercentHistory[i], _yellowPercentHistory[i], _drawPercentHistory[i]);
            }
        });
    }
}