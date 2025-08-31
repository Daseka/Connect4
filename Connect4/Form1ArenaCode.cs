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
    private const double ExplorationConstant = 1.25;
    private const int McstIterations = 800;
    private const string OldPolicyNetwork = "telemetry\\old_policy_network.json";
    private const string OldValueNetwork = "telemetry\\old_value_network.json";
    private const int SelfPlayGames = 200;
    private const int TrainingDataCount = 512;
    private const string Unknown = "Random";
    private const int VsGames = 300;
    private readonly AgentCatalog _agentCatalog;
    private readonly List<double> _drawPercentHistory = [];
    private readonly List<double> _redPercentHistory = [];
    private readonly TelemetryHistory _telemetryHistory = new();
    private readonly List<double> _yellowPercentHistory = [];
    private Agent? _currentAgent;
    private double _drawPercent;
    private double _redPercent;
    private double _redWithDrawPercent;
    private double _yellowWithDrawPercent;
    private double _yellowPercent;

    public async Task BattleArena()
    {
        int bossLives = BossLifeMaximum;
        int bossWinStreak = 0;
        bool skipTraining = false;
        int i = 0;
        Mcts? trainedRedMcts = null;

        _currentAgent = CreateAgent(ExplorationConstant, _yellowMcts, _currentAgent);
        _agentCatalog.Add(_currentAgent);

        while (i < ArenaIterations && !_arenaCancelationSource.IsCancellationRequested)
        {
            i++;
            if (!skipTraining)
            {
                // Reset New entry count telemetry history
                _telemetryHistory.BeginAddingNewEntries();

                // play a minimum amont of self play games to refresh the telemetry history
                var stopwatch = Stopwatch.StartNew();
                await SelfPlayParallel(_currentAgent, SelfPlayGames);
                stopwatch.Stop();
                _ = BeginInvoke(() => 
                {
                    toolStripStatusLabel1.Text = $"Self play completed in {stopwatch.ElapsedMilliseconds} ms";
                });

                if (_arenaCancelationSource.IsCancellationRequested)
                {
                    _ = BeginInvoke(() => listBox1.Items.Add("Battle Arena cancelled."));
                    listBox1.TopIndex = listBox1.Items.Count - 1;
                    return;
                }

                // Train a new red network
                stopwatch = Stopwatch.StartNew();
                trainedRedMcts = new Mcts(McstIterations, _redMcts.ValueNetwork!.Clone(), _redMcts.PolicyNetwork!.Clone());
                _ = await TrainAsync(trainedRedMcts);
                stopwatch.Stop();
                _ = BeginInvoke(() =>
                {
                    listBox1.Items.Add($"Training completed in {stopwatch.ElapsedMilliseconds} ms");
                    listBox1.TopIndex = listBox1.Items.Count - 1;
                });
            }

            skipTraining = false;

            // Evaluate the trained network against the current agent
            var stopwatchEval = Stopwatch.StartNew();
            double redPercentAfterTraining = await EvaluateAgent(trainedRedMcts);
            stopwatchEval.Stop();
            _ = BeginInvoke(() =>
            {
                listBox1.Items.Add($"Evaluation completed in {stopwatchEval.ElapsedMilliseconds} ms");
                listBox1.TopIndex = listBox1.Items.Count - 1;
            });

            if (redPercentAfterTraining > DeepLearningThreshold)
            {
                // Boss has lost reset win streak and decrease exploration factor
                bossWinStreak = 0;

                if (bossLives <= 1)
                {
                    _redMcts = trainedRedMcts ?? _redMcts;
                    _currentAgent = CreateAgent(ExplorationConstant, _redMcts, _currentAgent);

                    _agentCatalog.Add(_currentAgent);

                    _yellowMcts = new Mcts(McstIterations, _currentAgent.ValueNetwork, _currentAgent.PolicyNetwork);

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
                }

                bossLives += bossLives < BossLifeMaximum ? 1 : 0;
                _ = BeginInvoke(() =>
                {
                    _ = listBox1.Items.Add($"Boss Lives {bossLives}: boss unfased need more training");
                    listBox1.TopIndex = listBox1.Items.Count - 1;
                });
            }
        }
    }

    private static Agent CreateAgent(double explorationFactor, Mcts mcts, Agent? previousAgent)
    {
        //Create an agent based on the victorious Red
        IStandardNetwork valueNetwork = mcts.ValueNetwork?.Clone()!;
        valueNetwork.ExplorationFactor = explorationFactor;
        IStandardNetwork policyNetwork = mcts.PolicyNetwork?.Clone()!;
        policyNetwork.ExplorationFactor = explorationFactor;

        var currentAgent = new Agent
        {
            Id = Guid.NewGuid().ToString()[..8],
            ValueNetwork = valueNetwork,
            PolicyNetwork = policyNetwork,
            Created = DateTime.Now.ToString("yyyy-MM-dd HH:mm"),
            FirstKill = previousAgent?.Id ?? Unknown,
            Generation = previousAgent?.Generation + 1 ?? 1,
            ExplorationFactor = explorationFactor
        };

        return currentAgent;
    }

    private async Task<double> EvaluateAgent(Mcts? trainedRedMcts)
    {
        // Evaluate the trained network against the current agent
        _ = BeginInvoke(() =>
        {
            _ = listBox1.Items.Add(string.Empty);
            _ = listBox1.Items.Add($"Playing Challenger Vs Champ");
            listBox1.TopIndex = listBox1.Items.Count - 1;
        });

        var (red, yellow, draw, total)= await VsPlayParallel(trainedRedMcts!, _yellowMcts, McstIterations, ExplorationConstant);
        var redWin1 = _redWithDrawPercent;
        var yellowWin1 = _yellowWithDrawPercent;
        var draws = draw;
        var totalGames = total;


        _ = BeginInvoke(() =>
        {
            _ = listBox1.Items.Add($"Challenger {_redWithDrawPercent}% Champ {_yellowWithDrawPercent}%");
            listBox1.TopIndex = listBox1.Items.Count - 1;
        });

        // Now Evaluate the current agent against the trained network
        _ = BeginInvoke(() =>
        {
            _ = listBox1.Items.Add($"Playing Champ Vs Challenger");
            listBox1.TopIndex = listBox1.Items.Count - 1;
        });

        (red, yellow, draw, total)  = await VsPlayParallel(_yellowMcts, trainedRedMcts!, McstIterations, ExplorationConstant);
        var redWin2 = _yellowWithDrawPercent;
        var yellowWin2 = _redWithDrawPercent;
        draws += draw;
        totalGames += total;

        _ = BeginInvoke(() =>
        {
            _ = listBox1.Items.Add($"Challenger {_yellowWithDrawPercent}% Champ {_redWithDrawPercent}%");
            listBox1.TopIndex = listBox1.Items.Count - 1;
        });

        //Draw the chart
        var redPercentAfterTraining = Math.Min(redWin2, redWin1);
        var yellowPercentAfterTraining = Math.Min(yellowWin2, yellowWin1);
        _redPercentHistory.Add(redPercentAfterTraining);
        _yellowPercentHistory.Add(yellowPercentAfterTraining);
        _drawPercentHistory.Add(Math.Round(draws / (double)totalGames * 100, 2));
        UpdatePercentChart(DeepLearningThreshold);

        _ = BeginInvoke(() =>
        {
            _ = listBox1.Items.Add($"Final Result Challenger {redPercentAfterTraining}% Champ {yellowPercentAfterTraining}%");
            listBox1.TopIndex = listBox1.Items.Count - 1;
        });

        return redPercentAfterTraining;
    }

    private async Task SelfPlayParallel(Agent agent, int numberOfGames)
    {
        CancellationToken cancellationToken = _arenaCancelationSource.Token;
        int processorCount = Environment.ProcessorCount;
        int parallelGames = Math.Max(2, processorCount);

        int totalGames = numberOfGames > 0 ? numberOfGames : SelfPlayGames;

        int gamesPerThread = totalGames / parallelGames;
        int remainder = totalGames % parallelGames;

        Invoke(() =>
        {
            _gamePanels.Clear();
            flowLayoutPanel1.Controls.Clear();
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
        int gameCount = 0;

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
                    int gamesPlayed = 0;
                    int redWins = 0;
                    int yellowWins = 0;
                    int draws = 0;

                    GamePanel panel = _gamePanels[index];
                    CompactConnect4Game game = panel.Game;
                    PictureBox pictureBox = panel.PictureBox;

                    var redMcts = new Mcts(McstIterations, agent.ValueNetwork!.Clone(), agent.PolicyNetwork!.Clone());
                    var yellowMcts = new Mcts(McstIterations, agent.ValueNetwork.Clone(), agent.PolicyNetwork.Clone());

                    while (!cancellationToken.IsCancellationRequested && gamesPlayed < gamesToPlay)
                    {
                        bool gameEnded = false;
                        int playedMoves = 0;

                        while (!gameEnded && !cancellationToken.IsCancellationRequested)
                        {
                            Mcts mcts = game.CurrentPlayer == (int)Player.Red
                                ? redMcts
                                : yellowMcts;

                            int move = await mcts.GetBestMove(
                                game.GameBoard, 
                                (int)game.GameBoard.LastPlayed, 
                                ExplorationConstant,
                                playedMoves);

                            if (move == -1)
                            {
                                redMcts.SetWinnerTelemetryHistory(Winner.Draw);
                                yellowMcts.SetWinnerTelemetryHistory(Winner.Draw);

                                gameEnded = true;

                                draws++;
                                gamesPlayed++;

                                _ = BeginInvoke(() =>
                                {
                                    gameCount++;
                                    Text = $"Games played {gameCount}/{totalGames} Data: {_telemetryHistory.Count} New: {_telemetryHistory.NewEntries}";

                                    panel.RecordResult(Winner.Draw);
                                    pictureBox.Refresh();
                                });

                                globalStats[index] = (redWins, yellowWins, draws, gamesPlayed);
                                game.ResetGame();

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

                                _ = BeginInvoke(() =>
                                {
                                    gameCount++;
                                    Text = $"Games played {gameCount}/{totalGames} Data: {_telemetryHistory.Count} New: {_telemetryHistory.NewEntries}";

                                    panel.RecordResult(game.Winner);
                                    pictureBox.Refresh();
                                });

                                globalStats[index] = (redWins, yellowWins, draws, gamesPlayed);
                                game.ResetGame();

                                continue;
                            }

                            playedMoves++;
                        }
                    }

                    lock (sharedTelemetryHistory)
                    {
                        BeginInvoke(() => Text = $"Games played {gameCount}/{totalGames} Data: {_telemetryHistory.Count} New: {_telemetryHistory.NewEntries}");

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

        int sampleSize = Math.Min(_telemetryHistory.Count, (int)Math.Round(_telemetryHistory.NewEntries * 2.0));

        double previousError = double.MaxValue;
        double previousError2 = double.MaxValue;
        var errorHistory = new Queue<double>();
        var error2History = new Queue<double>();
        double? previousMovingAverageError = null;
        double? previousMovingAverageError2 = null;
        int consecutiveIncreasesError = 0;
        int consecutiveIncreasesError2 = 0;
        double error = 0;
        double error2 = 0;
        bool stopEarly = false;
        bool stopEarly2 = false;
        IStandardNetwork? tempValueNetwork = mcts.ValueNetwork!.Clone();
        IStandardNetwork? tempPolicyNetwork = mcts.PolicyNetwork!.Clone();

        (double[][] trainingData, double[][] policyExpectedData, double[][] valueExpectedData) = telemetryHistory
                .GetTrainingDataRandom(sampleSize);
        //.GetTrainingData(sampleSize);

        int i = -1;
        //for (int i = 0; i < steps; i++)
        while (i < 5)
        {
            i++;

            if (!stopEarly)
            {
                error = valueTrainer.Train(trainingData, valueExpectedData);
            }

            if (!stopEarly2)
            {
                error2 = policyTrainer.Train(trainingData, policyExpectedData);
            }

            string arrow = error > previousError ? "🡹" : "🡻";
            string arrow2 = error2 > previousError2 ? "🡹" : "🡻";

            Invoke(() =>
            {
                _ = listBox1.Items.Add($"V:Error {Math.Round(error, 8):F8} {arrow} \t P:Error {Math.Round(error2, 8):F8} {arrow2}\t Step {i}");
                listBox1.TopIndex = listBox1.Items.Count - 1;
            });

            if (errorHistory.Count == 50)
            {
                errorHistory.Dequeue();
            }
            errorHistory.Enqueue(error);

            if (error2History.Count == 50)
            {
                error2History.Dequeue();
            }
            error2History.Enqueue(error2);

            // Calculate moving averages
            double movingAverageError = errorHistory.Average();
            double movingAverageError2 = error2History.Average();

            if (previousMovingAverageError.HasValue && movingAverageError > previousMovingAverageError)
            {
                consecutiveIncreasesError++;
            }
            else if (!stopEarly)
            {
                tempValueNetwork = mcts.ValueNetwork!.Clone();
                consecutiveIncreasesError = 0;
            }

            if (previousMovingAverageError2.HasValue && movingAverageError2 > previousMovingAverageError2)
            {
                consecutiveIncreasesError2++;
            }
            else if (!stopEarly2)
            {
                tempPolicyNetwork = mcts.PolicyNetwork!.Clone();
                consecutiveIncreasesError2 = 0;
            }

            // only start checking for early stoping after a few steps to allow some initial training
            if (i > 30)
            {
                previousMovingAverageError = movingAverageError;
                previousMovingAverageError2 = movingAverageError2;
            }

            if (!stopEarly && consecutiveIncreasesError >= 2)
            {
                Invoke(() =>
                {
                    _ = listBox1.Items.Add("Training V stopped due to consecutive increases in moving averages.");
                    listBox1.TopIndex = listBox1.Items.Count - 1;
                });
                stopEarly = true;
            }

            // Break the loop if either moving average increases twice in a row
            if (!stopEarly2 && consecutiveIncreasesError2 >= 2)
            {
                Invoke(() =>
                {
                    _ = listBox1.Items.Add("Training P stopped due to consecutive increases in moving averages.");
                    listBox1.TopIndex = listBox1.Items.Count - 1;
                });
                stopEarly2 = true;
            }

            if (stopEarly && stopEarly2)
            {
                break;
            }

            previousError = error;
            previousError2 = error2;
        }

        mcts.PolicyNetwork = tempPolicyNetwork;
        mcts.ValueNetwork = tempValueNetwork;

        string y = $"Current agent is {_currentAgent?.Id ?? "None"} generation: {_currentAgent?.Generation ?? 0}";
        Invoke(() =>
        {
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
                _redWithDrawPercent = Math.Round(_redPercent + _drawPercent /2, 2);
                _yellowWithDrawPercent = Math.Round(_yellowPercent + _drawPercent /2, 2);

                Text = $"{totalGames}/{totalGamesToPlay} - " +
                    $"R: {_redPercent:F2}% ({_redWithDrawPercent:F2}%)" +
                    $"Y: {_yellowPercent:F2}% ({_yellowWithDrawPercent:F2}%)" +
                    $"D: {_drawPercent:F2}% " +
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

    private async Task<(int redWins, int yellowWins, int drawWins, int totalWins)> 
        VsPlayParallel(Mcts mctsRed, Mcts mctsYellow, int mcstIterations, double explorationFactor)
    {
        CancellationToken cancellationToken = _arenaCancelationSource.Token;

        int processorCount = Environment.ProcessorCount;
        int parallelGames = Math.Max(2, processorCount - 1);

        int totalGames = VsGames;

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
        var globalStats = new ConcurrentDictionary<int, (int redWins, int yellowWins, int drawWins, int totalWins)>();

        for (int gameIndex = 0; gameIndex < parallelGames; gameIndex++)
        {
            int index = gameIndex;
            globalStats[index] = (0, 0, 0, 0);

            // Calculate how many games this thread should play
            // First 'remainingGames' threads get one extra game
            int gamesToPlay = gamesPerThread + (index < remainder ? 1 : 0);

            tasks.Add(Task.Run(async () =>
            {
                int gamesPlayed = 0;
                int redWins = 0;
                int yellowWins = 0;
                int draws = 0;

                GamePanel panel = _gamePanels[index];
                CompactConnect4Game game = panel.Game;
                PictureBox pictureBox = panel.PictureBox;

                var redMcts = new Mcts(mcstIterations, mctsRed.ValueNetwork.Clone(), mctsRed.PolicyNetwork.Clone());
                var yellowMcts = new Mcts(mcstIterations, mctsYellow.ValueNetwork.Clone(), mctsYellow.PolicyNetwork.Clone());

                while (!cancellationToken.IsCancellationRequested && gamesPlayed < gamesToPlay)
                {
                    bool gameEnded = false;

                    while (!gameEnded && !cancellationToken.IsCancellationRequested)
                    {
                        Mcts mcts = game.CurrentPlayer == (int)Player.Red
                            ? redMcts
                            : yellowMcts;

                        int move = await mcts.GetBestMove(
                            game.GameBoard,
                            (int)game.GameBoard.LastPlayed, 
                            explorationFactor,
                            0,
                            true);

                        if (move == -1)
                        {

                            draws++;
                            gamesPlayed++;

                            _ = BeginInvoke(() =>
                            {
                                panel.RecordResult(Winner.Draw);
                                pictureBox.Refresh();
                            });

                            game.ResetGame();
                            gameEnded = true;

                            globalStats[index] = (redWins, yellowWins, draws, gamesPlayed);
                            _ = BeginInvoke(() => UpdateGlobalStats(globalStats, totalGames));

                            continue;
                        }

                        int winner = game.PlacePieceColumn(move);
                        _ = BeginInvoke(() => pictureBox.Refresh());

                        if (winner != 0)
                        {

                            if (winner == 1)
                            {
                                redWins++;
                            }
                            else
                            {
                                yellowWins++;
                            }

                            gamesPlayed++;

                            _ = BeginInvoke(() =>
                            {
                                Winner result = game.Winner;
                                panel.RecordResult(result);
                            });
                            globalStats[index] = (redWins, yellowWins, draws, gamesPlayed);
                            _ = BeginInvoke(() => UpdateGlobalStats(globalStats, totalGames));

                            game.ResetGame();
                            gameEnded = true;

                            _ = BeginInvoke(() => pictureBox.Refresh());

                            continue;
                        }
                    }
                }
            }));
        }

        await Task.WhenAll(tasks).ConfigureAwait(false);

        _ = BeginInvoke(() => UpdateGlobalStats(globalStats, totalGames));

        _ = BeginInvoke(() =>
        {
            if (!cancellationToken.IsCancellationRequested)
            {
                toolStripStatusLabel1.Text = "All parallel games completed!";
                button4.Text = "Parallel Play";
                _isParallelSelfPlayRunning = false;
            }
        });

        // aggragate global stats to 1 final result
        int red = 0, yellow = 0, draw = 0, total = 0;
        foreach (var stat in globalStats.Values)
        {
            red += stat.redWins;
            yellow += stat.yellowWins;
            draw += stat.drawWins;
            total += stat.totalWins;
        }

        return (red, yellow, draw, total);
    }
}