using Connect4.Ais;
using Connect4.GameParts;
using DeepNetwork;
using System.Collections.Concurrent;
using System.Diagnostics;

namespace Connect4;

public partial class Form1 : Form
{
    private const int ArenaIterations = 300;
    private const int DeepLearningThreshold = 51;
    private const double MaximumError = 0.10;
    private const int McstIterations = 1600;
    private const int MinimumGameCount = 4;
    private const string OldPolicyNetwork = "telemetry\\old_policy_network.json";
    private const string OldValueNetwork = "telemetry\\old_value_network.json";
    private const int SelfPlayGames = 100;
    private const int TelemetryHistorySaturation = 1000;
    private const int TrainingDataCount = 1000;
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
        double maximumError = MaximumError;

        int i = 0;
        while (i < ArenaIterations && !_arenaCancelationToken.IsCancellationRequested)
        {
            i++;

            // play atleast approximitly 10k games to have enough data
            int initialGames = _telemetryHistory.Count < TelemetryHistorySaturation
                ? TelemetryHistorySaturation / 10
                : 0;
            await SelfPlayParallelAsync(McstIterations, initialGames);

            if (_redWithDrawPercent >= DeepLearningThreshold)
            {
                if (bossLives <= 1)
                {
                    bossLives = 3;

                    //Only save the networks if Red has a win rate above deep learning threshold
                    NetworkSaver.SaveNetwork(_redMcts.ValueNetwork, OldValueNetwork);
                    NetworkSaver.SaveNetwork(_redMcts.PolicyNetwork, OldPolicyNetwork);

                    // Load the higher win rate networks for Yellow
                    _oldValueNetwork = NetworkLoader.LoadNetwork(OldValueNetwork) ?? _oldValueNetwork;
                    _oldPolicyNetwork = NetworkLoader.LoadNetwork(OldPolicyNetwork) ?? _oldPolicyNetwork;

                    _yellowMcts = new Mcts(McstIterations, _oldValueNetwork, _oldPolicyNetwork);

                    _ = BeginInvoke(() => toolStripStatusLabel1.Text = "Boss Dead: Yellow has new network");
                }
                else
                {
                    skipTraining = true;
                    bossLives--;
                    _ = BeginInvoke(() => toolStripStatusLabel1.Text = $"Boss Lives {bossLives}: Reduced boss life skipping training");
                }
            }
            else
            {
                bossLives += bossLives < 3 ? 1 : 0;
                _ = BeginInvoke(() => toolStripStatusLabel1.Text = $"Boss Lives {bossLives}: boss unfased need more training");
            }

            if (_arenaCancelationToken.IsCancellationRequested)
            {
                _ = BeginInvoke(() => toolStripStatusLabel1.Text = "Battle Arena cancelled.");
                return;
            }

            if (!skipTraining)
            {
                _ = await TrainAsync(_redMcts, maximumError);
            }

            skipTraining = false;
        }
    }

    private async Task SelfPlayParallelAsync(int mcstIterations, int initialGames = 0)
    {
        _cancellationTokenSource = new CancellationTokenSource();
        CancellationToken cancellationToken = _cancellationTokenSource.Token;

        int processorCount = Environment.ProcessorCount;
        int parallelGames = Math.Max(2, processorCount - 1);

        int totalGames = initialGames > 0 ? initialGames : SelfPlayGames;

        int gamesPerThread = totalGames / parallelGames;
        int remainder = totalGames % parallelGames;

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
                                redMcts.SetWinnerTelemetryHistory(Winner.Draw);
                                yellowMcts.SetWinnerTelemetryHistory(Winner.Draw);
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
                                _ = BeginInvoke(() => UpdateGlobalStats(globalStats, totalGames));

                                continue;
                            }

                            int winner = game.PlacePieceColumn(move);
                            _ = BeginInvoke(() => pictureBox.Refresh());

                            if (winner != 0)
                            {
                                redMcts.SetWinnerTelemetryHistory(game.Winner);
                                yellowMcts.SetWinnerTelemetryHistory(game.Winner);
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
                                _ = BeginInvoke(() => UpdateGlobalStats(globalStats, totalGames));

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

        _ = BeginInvoke(() => UpdateGlobalStats(globalStats, totalGames));
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
        Invoke(listBox1.Items.Clear);

        TelemetryHistory telemetryHistory = _telemetryHistory;
        var random = new Random();
        int minPolicRuns = int.MaxValue;
        double minPolicyError = double.MaxValue;

        INetworkTrainer valueTrainer = NetworkTrainerFactory.CreateNetworkTrainer(mcts.ValueNetwork);
        INetworkTrainer policyTrainer = NetworkTrainerFactory.CreateNetworkTrainer(mcts.PolicyNetwork);

        int steps = 100;
        var stopwatch = Stopwatch.StartNew();

        // get training data inputs and expected outputs that have minimum game count
        (double[][] trainingData, double[][] policyExpectedData, double[][] valueExpectedData) = telemetryHistory
            .GetTrainingData(TrainingDataCount, MinimumGameCount);

        for (int i = 0; i < steps; i++)
        {
            double error = valueTrainer.Train(trainingData, valueExpectedData);
            Invoke(() =>
            {
                _ = listBox1.Items.Add($"V:Error {Math.Round(error, 8):F8}");
                listBox1.TopIndex = listBox1.Items.Count - 1;
            });

            double error2 = policyTrainer.Train(trainingData, policyExpectedData);
            Invoke(() =>
            {
                _ = listBox1.Items.Add($"P:Error {Math.Round(error2, 8):F8}");
                listBox1.TopIndex = listBox1.Items.Count - 1;
            });
        }

        stopwatch.Stop();

        string x = $"Training value completed in {stopwatch.ElapsedMilliseconds} ms";
        Invoke(() =>
        {
            _ = listBox1.Items.Add(x);
            listBox1.TopIndex = listBox1.Items.Count - 1;
        });

        return Task.FromResult<(int, double)>((minPolicRuns, minPolicyError));
    }

    private void UpdateGlobalStats(
        ConcurrentDictionary<int, (int red, int yellow, int draw, int total)> globalStats,
        int totalGamesToPlay)
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

                Text = $"{totalGames}/{totalGamesToPlay} - " +
                    $"R: {_redPercent:F2}% ({_redWithDrawPercent:F2}%)" +
                    $"Y: {_yellowPercent:F2}% " +
                    $"D: {_drawPercent:F2}%" +
                    $"Data: {_telemetryHistory.Count}";

                int progressPercent = (int)(totalGames / (double)totalGamesToPlay * 100);
                toolStripStatusLabel1.Text = $"Running: {totalGames}/{totalGamesToPlay} games completed ({progressPercent}%)";
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