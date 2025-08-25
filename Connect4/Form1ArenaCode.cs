using Connect4.Ais;
using Connect4.GameParts;
using DeepNetwork;
using DeepNetwork.NetworkIO;
using System.Collections.Concurrent;
using System.Diagnostics;

namespace Connect4;

public partial class Form1 : Form
{
    private const int ArenaIterations = 300;
    private const int BossLifeMaximum = 3;
    private const int BossWinStreakMaximum = 5;
    private const int DeepLearningThreshold = 55;
    private const double ExplorationConstantMaximum = 5.40;
    private const double ExplorationConstantMinimum = 1.40;
    private const int McstIterations = 400;
    private const int MinimumGameCount = 4;
    private const string OldPolicyNetwork = "telemetry\\old_policy_network.json";
    private const string OldValueNetwork = "telemetry\\old_value_network.json";
    private const int SelfPlayGames = 100;
    private const int TelemetryHistorySaturation = 1000;
    private const int TrainingDataCount = 1000;
    private const string Unknown = "Random";
    private readonly AgentCatalog _agentCatalog;
    private readonly List<double> _drawPercentHistory = [];
    private readonly List<double> _redPercentHistory = [];
    private readonly TelemetryHistory _telemetryHistory = new();
    private readonly List<double> _yellowPercentHistory = [];
    private Agent? _currentAgent;
    private double _drawPercent;
    private double _redPercent;
    private double _redWithDrawPercent;
    private double _yellowPercent;

    private async Task BattleArena()
    {
        int bossLives = BossLifeMaximum;
        int bossWinStreak = 0;
        bool skipTraining = false;
        double explorationFactor = ExplorationConstantMaximum;
        int i = 0;
        while (i < ArenaIterations && !_arenaCancelationSource.IsCancellationRequested)
        {
            i++;

            // play a minimum amont of games to have enough data
            int initialGames = _telemetryHistory.Count < TelemetryHistorySaturation
                ? TelemetryHistorySaturation / 10
                : 0;
            await SelfPlayParallelAsync(McstIterations, explorationFactor, initialGames);

            if (_redWithDrawPercent >= DeepLearningThreshold)
            {
                // Boss has lost reset win streak and decrease exploration factor
                bossWinStreak = 0;

                if (bossLives <= 1)
                {
                    //Create an agent based on the victorious Red
                    IStandardNetwork valueNetwork = _redMcts.ValueNetwork?.Clone()!;
                    valueNetwork.ExplorationFactor = explorationFactor;
                    IStandardNetwork policyNetwork = _redMcts.PolicyNetwork?.Clone()!;
                    policyNetwork.ExplorationFactor = explorationFactor;

                    _currentAgent = new Agent
                    {
                        Id = Guid.NewGuid().ToString()[..8],
                        ValueNetwork = valueNetwork,
                        PolicyNetwork = policyNetwork,
                        Created = DateTime.Now.ToString("yyyy-MM-dd HH:mm"),
                        FirstKill = _currentAgent?.Id ?? Unknown,
                        Generation = _currentAgent?.Generation + 1 ?? 1,
                        ExplorationFactor = explorationFactor
                    };

                    _agentCatalog.Add(_currentAgent);

                    _yellowMcts = new Mcts(McstIterations, _currentAgent.ValueNetwork, _currentAgent.PolicyNetwork);
                    
                    explorationFactor = ExplorationConstantMinimum;
                    bossLives = 3;

                    _ = BeginInvoke(() => 
                    { 
                        _ = listBox1.Items.Add("Boss Dead: Yellow has new network");
                        listBox1.TopIndex = listBox1.Items.Count - 1;
                    });
                }
                else
                {
                    skipTraining = true;
                    bossLives--;
                    _ = BeginInvoke(() =>
                    {
                        _ = listBox1.Items.Add($"Boss Lives {bossLives}: Reduced boss life skipping training");
                        listBox1.TopIndex = listBox1.Items.Count - 1;
                    });
                }
            }
            else
            {
                bossWinStreak++;
                if (bossWinStreak >= BossWinStreakMaximum)
                {
                    bossWinStreak = 0;
                    explorationFactor = Math.Min(ExplorationConstantMaximum, explorationFactor + 1);
                }

                bossLives += bossLives < BossLifeMaximum ? 1 : 0;
                _ = BeginInvoke(() =>
                { 
                    _ = listBox1.Items.Add($"Boss Lives {bossLives}: boss unfased need more training");
                    listBox1.TopIndex = listBox1.Items.Count - 1;
                });
            }

            if (_arenaCancelationSource.IsCancellationRequested)
            {
                _ = BeginInvoke(() => toolStripStatusLabel1.Text = "Battle Arena cancelled.");
                return;
            }

            if (!skipTraining)
            {
                _ = await TrainAsync(_redMcts);

                _ = BeginInvoke(() =>
                {
                    _ = listBox1.Items.Add($"Boss lives: {bossLives} \t Boss win streak {bossWinStreak}");
                    _ = listBox1.Items.Add($"Exploration factor {explorationFactor:F2}");
                    
                    listBox1.TopIndex = listBox1.Items.Count - 1;
                });
            }

            skipTraining = false;
        }
    }

    private async Task SelfPlayParallelAsync(int mcstIterations, double explorationFactor, int initialGames = 0)
    {
        CancellationToken cancellationToken = _arenaCancelationSource.Token;

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

                            double factor;
                            if (game.CurrentPlayer == (int)Player.Red)
                            {
                                factor = (double)explorationFactor;
                            }
                            else
                            {
                                factor = mcts.ValueNetwork?.ExplorationFactor > 0
                                    ? (double)mcts.ValueNetwork?.ExplorationFactor!
                                    : explorationFactor;
                            }

                            int move = await mcts.GetBestMove(game.GameBoard, (int)game.GameBoard.LastPlayed, factor);

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

    private Task<(int runs, double error)> TrainAsync(Mcts mcts)
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

        double previousError = double.MaxValue;
        double previousError2 = double.MaxValue;
        for (int i = 0; i < steps; i++)
        {
            double error = valueTrainer.Train(trainingData, valueExpectedData);
            double error2 = policyTrainer.Train(trainingData, policyExpectedData);

            string arrow = error > previousError ? "🡹" : "🡻";
            string arrow2 = error2 > previousError2 ? "🡹" : "🡻";

            Invoke(() =>
            {
                _ = listBox1.Items.Add($"V:Error {Math.Round(error, 8):F8} {arrow} \t P:Error {Math.Round(error2, 8):F8} {arrow2}\t Step {i}");
                listBox1.TopIndex = listBox1.Items.Count - 1;
            });

            previousError = error;
            previousError2 = error2;
        }

        stopwatch.Stop();

        string x = $"Training on {trainingData.Length} items completed in {stopwatch.ElapsedMilliseconds} ms";
        string y = $"Current agent is {_currentAgent?.Id ?? "None"} generation: {_currentAgent?.Generation ?? 0}";
        Invoke(() =>
        {
            _ = listBox1.Items.Add(x);
            _ = listBox1.Items.Add(y);
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