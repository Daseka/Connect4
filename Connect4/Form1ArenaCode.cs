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
    private const int ChampionsToPlay = 6;
    private const int ConsecutiveIncreaseLimit = 2;
    private const int DeepLearningThreshold = 55;
    private const int DeepLearningThresholdMin = 35;
    private const double ErrorConfidence = 1.96;
    private const double ExplorationConstant = 1.40;
    private const int McstIterations = 400;
    private const int MovingAverageSize = 50;
    private const int SelfPlayGames = 300;
    private const string Unknown = "Random";
    private const int VsGames = 100;
    private const int VirtualEpochCount = 5;
    private readonly AgentCatalog _agentCatalog;
    private readonly List<double> _drawPercentHistory = [];
    private readonly List<double> _redPercentHistory = [];
    private readonly TrainingBuffer _trainingBuffer = new();
    private readonly ReplayBuffer _replayBuffer = new();
    private readonly List<double> _yellowPercentHistory = [];
    private Agent? _currentAgent;
    private double _drawPercent;
    private double _redPercent;
    private double _redWithDrawPercent;
    private double _yellowPercent;
    private double _yellowWithDrawPercent;

    public async Task BattleArena()
    {
        ResetChart();

        bool skipTraining = false;
        int i = 0;
        Mcts? trainedAgent = null;

        // Get the current best agent from the catalog or create a new one if none exists
        List<Agent> championAgents = _agentCatalog.GetLatestAgents(ChampionsToPlay);
        if (championAgents.Count == 0)
        {
            _currentAgent = CreateAgent(ExplorationConstant, _yellowMcts, _currentAgent);
            _agentCatalog.Add(_currentAgent);
            championAgents.Add(_currentAgent);
        }
        else
        {
            _currentAgent = championAgents.First();
        }

        int championsRemaining = Math.Min(championAgents.Count,ChampionsToPlay);

        while (i < ArenaIterations && !_arenaCancelationSource.IsCancellationRequested)
        {
            i++;
            if (!skipTraining)
            {
                // Reset New entry count 
                _trainingBuffer.BeginAddingNewEntries();

                // play a minimum amont of self play games to refresh the training buffer
                var stopwatch = Stopwatch.StartNew();
                await SelfPlayParallel(_currentAgent, SelfPlayGames);
                stopwatch.Stop();

                if (_arenaCancelationSource.IsCancellationRequested)
                {
                    _ = BeginInvoke(() =>
                    {
                        _ = listBox1.Items.Add("Battle Arena cancelled.");
                        listBox1.TopIndex = listBox1.Items.Count - 1;
                    });

                    return;
                }

                // Train a new red network
                stopwatch = Stopwatch.StartNew();
                trainedAgent = new Mcts(McstIterations, _currentAgent.ValueNetwork!.Clone(), _currentAgent.PolicyNetwork!.Clone());
                _ = await TrainAsync(trainedAgent);
                stopwatch.Stop();

                _ = BeginInvoke(() =>
                {
                    _ = listBox1.Items.Add($"Training on Selfplay done in {stopwatch.ElapsedMilliseconds} ms");
                    listBox1.TopIndex = listBox1.Items.Count - 1;
                });
            }

            skipTraining = false;

            // Evaluate the trained network against the current agent
            var stopwatch2 = Stopwatch.StartNew();
            bool isBetter = await EvaluateAgent(trainedAgent);
            stopwatch2.Stop();

            _ = BeginInvoke(() =>
            {
                _ = listBox1.Items.Add($"Evaluation done in {stopwatch2.ElapsedMilliseconds} ms");
                listBox1.TopIndex = listBox1.Items.Count - 1;
            });

            if (isBetter)
            {
                if (championsRemaining <= 1)
                {
                    _redMcts = trainedAgent ?? _redMcts;

                    _agentCatalog.Add(CreateAgent(ExplorationConstant, _redMcts, championAgents.First()));
                    championAgents = _agentCatalog.GetLatestAgents(ChampionsToPlay);
                    _currentAgent = championAgents.First();


                    championsRemaining = championAgents.Count;

                    AdjustTrainingSetAndReplayBuffer();

                    _ = BeginInvoke(() =>
                    {
                        _ = listBox1.Items.Add("Boss Dead: Yellow has new network");
                        listBox1.TopIndex = listBox1.Items.Count - 1;
                    });
                }
                else
                {

                    skipTraining = true;
                    championsRemaining--;

                    _currentAgent = championAgents[championAgents.Count - championsRemaining];
                    
                    _ = BeginInvoke(() =>
                    {
                        _ = listBox1.Items.Add($"Boss Lives {championsRemaining}: Reduced boss life skipping training");
                        listBox1.TopIndex = listBox1.Items.Count - 1;
                    });
                }
            }
            else
            {
                _currentAgent = championAgents.First();
                championsRemaining = Math.Min(championAgents.Count, ChampionsToPlay);

                _ = BeginInvoke(() =>
                {
                    _ = listBox1.Items.Add($"Boss Lives {championsRemaining}: boss unfased need more training");
                    listBox1.TopIndex = listBox1.Items.Count - 1;
                });
            }
        }
    }

    private void AdjustTrainingSetAndReplayBuffer()
    {
        // Move 20% of the most recent training set to the replay buffer
        int count = (int)(_trainingBuffer.BoardStateHistoricalInfos.Count * 0.2);
        if (count > 0)
        {
            var trainingBuffer = new TrainingBuffer();
            foreach (var entry in _trainingBuffer.BoardStateHistoricalInfos.Skip(_trainingBuffer.BoardStateHistoricalInfos.Count - count))
            {
                trainingBuffer.BoardStateHistoricalInfos.Enqueue(entry);
            }
            _replayBuffer.MergeFrom(trainingBuffer);
        }

        _trainingBuffer.ClearAll();
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

    /// <summary>
    /// Returns true if the trainedRedMcts is better than the current yellowMcts
    /// </summary>
    private async Task<bool> EvaluateAgent(Mcts? trainedRedMcts)
    {
        // Evaluate the trained network against the current agent
        _ = BeginInvoke(() =>
        {
            _ = listBox1.Items.Add(string.Empty);
            _ = listBox1.Items.Add($"Playing Challenger Vs Champ {_currentAgent?.Generation}");
            listBox1.TopIndex = listBox1.Items.Count - 1;
        });

        var currentMcts = new Mcts(McstIterations, _currentAgent?.ValueNetwork, _currentAgent?.PolicyNetwork);

        (int red, int yellow, int draw, int total) = await VsPlayParallel(trainedRedMcts!, currentMcts, McstIterations, ExplorationConstant);
        double agent1Game1 = _redWithDrawPercent;
        double agent2Game1 = _yellowWithDrawPercent;
        int draws = draw;
        int totalGames = total;

        _ = BeginInvoke(() =>
        {
            _ = listBox1.Items.Add($"Challenger {_redWithDrawPercent}% Champ {_yellowWithDrawPercent}%");
            listBox1.TopIndex = listBox1.Items.Count - 1;
        });

        // Now Evaluate the current agent against the trained network
        _ = BeginInvoke(() =>
        {
            _ = listBox1.Items.Add($"Playing Champ {_currentAgent?.Generation} Vs Challenger");
            listBox1.TopIndex = listBox1.Items.Count - 1;
        });

        (red, yellow, draw, total) = await VsPlayParallel(currentMcts, trainedRedMcts!, McstIterations, ExplorationConstant);
        double agent1Game2 = _yellowWithDrawPercent;
        double agent2Game2 = _redWithDrawPercent;
        draws += draw;
        totalGames += total;

        _ = BeginInvoke(() =>
        {
            _ = listBox1.Items.Add($"Challenger {_yellowWithDrawPercent}% Champ {_redWithDrawPercent}%");
            listBox1.TopIndex = listBox1.Items.Count - 1;
        });

        //Draw the chart
        double redPercentAfterTraining = Math.Min(agent1Game2, agent1Game1);
        double yellowPercentAfterTraining = Math.Min(agent2Game2, agent2Game1);
        _redPercentHistory.Add(redPercentAfterTraining);
        _yellowPercentHistory.Add(yellowPercentAfterTraining);
        _drawPercentHistory.Add(Math.Round(draws / (double)totalGames * 100, 2));

        _ = BeginInvoke(() =>
        {
            _ = listBox1.Items.Add($"Final Result Challenger {redPercentAfterTraining}% Champ {yellowPercentAfterTraining}%");
            listBox1.TopIndex = listBox1.Items.Count - 1;
        });

        // Check if the new agent wins more than threshold of the games on one side and is better than the current agent on the other side
        double agent1Max = Math.Max(agent1Game1, agent1Game2) / 100;
        double agent1Min = Math.Min(agent1Game1, agent1Game2) / 100;
        double agent2Min = Math.Min(agent2Game1, agent2Game2) / 100;

        double marginOfError = ErrorConfidence * Math.Sqrt(agent1Min * (1 - agent1Min) / VsGames);
        bool isBetter = (agent1Max * 100) > DeepLearningThreshold 
            && agent2Min + marginOfError < agent1Min 
            && (agent1Min * 100)> DeepLearningThresholdMin;

        UpdatePercentChart(DeepLearningThreshold, isBetter);

        return isBetter;
    }

    private void ResetChart()
    {
        winPercentChart.Reset();
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

        var sharedTrainingBuffer = new TrainingBuffer();
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
                            redMcts.SetWinnerTrainingBuffer(Winner.Draw);
                            yellowMcts.SetWinnerTrainingBuffer(Winner.Draw);

                            gameEnded = true;

                            draws++;
                            gamesPlayed++;

                            _ = BeginInvoke(() =>
                            {
                                gameCount++;
                                Text = $"Games played {gameCount}/{totalGames} Data: {_trainingBuffer.Count} New: {_trainingBuffer.NewEntries} Replay: {_replayBuffer.Count}";

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
                            redMcts.SetWinnerTrainingBuffer((Winner)winner);
                            yellowMcts.SetWinnerTrainingBuffer((Winner)winner);

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
                                Text = $"Games played {gameCount}/{totalGames} Data: {_trainingBuffer.Count} New: {_trainingBuffer.NewEntries} Replay: {_replayBuffer.Count}";

                                panel.RecordResult((Winner)winner);
                                pictureBox.Refresh();
                            });

                            globalStats[index] = (redWins, yellowWins, draws, gamesPlayed);
                            game.ResetGame();

                            continue;
                        }

                        playedMoves++;
                    }
                }

                lock (sharedTrainingBuffer)
                {
                    _ = BeginInvoke(() => Text = $"Games played {gameCount}/{totalGames} Data: {_trainingBuffer.Count} New: {_trainingBuffer.NewEntries} Replay: {_replayBuffer.Count}");

                    _trainingBuffer.MergeFrom(redMcts.GetTrainingBuffer());
                    _trainingBuffer.MergeFrom(yellowMcts.GetTrainingBuffer());
                }
            }));
        }

        await Task.WhenAll(tasks).ConfigureAwait(false);
    }

    private Task<(int runs, double error)> TrainAsync(Mcts mcts)
    {
        _ = mcts.ValueNetwork is null || mcts.PolicyNetwork is null
            ? throw new InvalidOperationException("Both Value and Policy networks must be set before training.")
            : 1;

        Invoke(listBox1.Items.Clear);

        var random = new Random();
        int minPolicRuns = int.MaxValue;
        double minPolicyError = double.MaxValue;

        INetworkTrainer valueTrainer = NetworkTrainerFactory.CreateNetworkTrainer(mcts.ValueNetwork);
        INetworkTrainer policyTrainer = NetworkTrainerFactory.CreateNetworkTrainer(mcts.PolicyNetwork);

        int sampleSize = _trainingBuffer.Count;

        double previousValueError = double.MaxValue;
        double previousPolicyError = double.MaxValue;
        var valueErrorHistory = new Queue<double>();
        var policyErrorHistory = new Queue<double>();
        double? previousMovingAverageValueError = double.MaxValue;
        double? previousMovingAveragePolicyError = double.MaxValue;
        int consecutiveIncreasesValueError = 0;
        int consecutiveIncreasesPolicyError = 0;
        double valueError = 0;
        double policyError = 0;
        double clonedAtValueError = 0;
        double clonedAtPolicyError = 0;
        bool valueStopEarly = false;
        bool policyStopEarly = false;
        IStandardNetwork? tempValueNetwork = mcts.ValueNetwork;
        tempValueNetwork.Trained = true;
        IStandardNetwork? tempPolicyNetwork = mcts.PolicyNetwork;
        tempPolicyNetwork.Trained = true;

        int movingAveragePolicy = MovingAverageSize;
        int movingAverageValue = MovingAverageSize;

        int i = -1;
        string vStop = string.Empty;
        string pStop = string.Empty;

        
        int steps = (int)Math
            .Max(1, (VirtualEpochCount * (_trainingBuffer.Count + _replayBuffer.Count * 0.2))/ MiniBatchNetworkTrainer.BatchSize);

        //while (i < steps || _trainingSet.Count > TelemetryHistory.MaxBufferSize /2)
        //while(i < steps || trainingData.Length > 100000)


        while (i < steps )
        {
            i++;

            (double[][] trainingData, double[][] policyExpectedData, double[][] valueExpectedData) = _trainingBuffer
                .GetTrainingDataRandom((int)(MiniBatchNetworkTrainer.BatchSize * 0.8));

            (double[][] trainingData2, double[][] policyExpectedData2, double[][] valueExpectedData2) = _replayBuffer
                .GetTrainingDataRandom((int)(MiniBatchNetworkTrainer.BatchSize * 0.2));

            trainingData = [.. trainingData, .. trainingData2];
            policyExpectedData = [.. policyExpectedData, .. policyExpectedData2];
            valueExpectedData = [.. valueExpectedData, .. valueExpectedData2];

            if (!valueStopEarly)
            {
                valueError = valueTrainer.Train(trainingData, valueExpectedData);
            }

            if (!policyStopEarly)
            {
                policyError = policyTrainer.Train(trainingData, policyExpectedData);
            }

            string valueArrow = valueError > previousValueError ? "🡹" : "🡻";
            string policyArrow = policyError > previousPolicyError ? "🡹" : "🡻";

            Invoke(() =>
            {
                _ = listBox1.Items.Add($"V:Error {Math.Round(valueError, 8):F8} {valueArrow} \t P:Error {Math.Round(policyError, 8):F8} {policyArrow}\t Step {i}");
                listBox1.TopIndex = listBox1.Items.Count - 1;
            });

            //multiply by 2 because value fluctuates more
            if (valueErrorHistory.Count == movingAverageValue)
            {
                _ = valueErrorHistory.Dequeue();
            }

            valueErrorHistory.Enqueue(valueError);

            if (policyErrorHistory.Count == movingAveragePolicy)
            {
                _ = policyErrorHistory.Dequeue();
            }

            policyErrorHistory.Enqueue(policyError);

            // only start checking for early stoping after a few steps to allow some initial training
            if (i > movingAverageValue)
            {
                double movingAverageValueError = valueErrorHistory.Average();

                if (movingAverageValueError > previousMovingAverageValueError)
                {
                    consecutiveIncreasesValueError++;
                }
                else if (!valueStopEarly)
                {
                    if (valueError < previousValueError)
                    {
                        tempValueNetwork = mcts.ValueNetwork!.Clone();
                        clonedAtValueError = valueError;
                    }

                    consecutiveIncreasesValueError = 0;
                }

                previousMovingAverageValueError = movingAverageValueError;
            }

            if (i > movingAveragePolicy)
            {
                double movingAveragePolicyError = policyErrorHistory.Average();

                if (movingAveragePolicyError > previousMovingAveragePolicyError)
                {
                    consecutiveIncreasesPolicyError++;
                }
                else if (!policyStopEarly)
                {
                    if (policyError < previousPolicyError)
                    {
                        tempPolicyNetwork = mcts.PolicyNetwork!.Clone();
                        clonedAtPolicyError = policyError;
                    }
                    
                    consecutiveIncreasesPolicyError = 0;
                }

                previousMovingAveragePolicyError = movingAveragePolicyError;
            }

            if (!valueStopEarly && consecutiveIncreasesValueError >= ConsecutiveIncreaseLimit)
            {
                Invoke(() =>
                {
                    vStop = $"V peeked => steps: {i} boardstates: {MiniBatchNetworkTrainer.BatchSize * i}";
                    _ = listBox1.Items.Add(vStop);
                    listBox1.TopIndex = listBox1.Items.Count - 1;
                });
                valueStopEarly = true;
            }

            if (!policyStopEarly && (consecutiveIncreasesPolicyError >= ConsecutiveIncreaseLimit ))
            {
                Invoke(() =>
                {
                    pStop = $"P peeked => steps: {i} boardstates: {MiniBatchNetworkTrainer.BatchSize * i}";
                    _ = listBox1.Items.Add(pStop);
                    listBox1.TopIndex = listBox1.Items.Count - 1;
                });
                policyStopEarly = true;
            }

            if (valueStopEarly && policyStopEarly)
            {
                break;
            }

            previousValueError = valueError;
            previousPolicyError = policyError;
        }

        mcts.PolicyNetwork = tempPolicyNetwork;
        mcts.ValueNetwork = tempValueNetwork;

        string y = $"Current agent is {_currentAgent?.Id ?? "None"} generation: {_currentAgent?.Generation ?? 0}";
        string clonedErrors = $"Cloned error at V:{Math.Round(clonedAtValueError, 8):F8} P:{Math.Round(clonedAtPolicyError, 8):F8}";
        Invoke(() =>
        {
            _ = listBox1.Items.Add(string.Empty);
            _ = listBox1.Items.Add(clonedErrors);
            _ = listBox1.Items.Add(vStop);
            _ = listBox1.Items.Add(pStop);
            _ = listBox1.Items.Add($"Total boarstates trained {MiniBatchNetworkTrainer.BatchSize * i}");
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
                _yellowWithDrawPercent = Math.Round(_yellowPercent + _drawPercent / 2, 2);

                Text = $"{totalGames}/{totalGamesToPlay} - " +
                    $"R: {_redPercent:F2}% ({_redWithDrawPercent:F2}%)" +
                    $"Y: {_yellowPercent:F2}% ({_yellowWithDrawPercent:F2}%)" +
                    $"D: {_drawPercent:F2}% " +
                    $"Data: {_trainingBuffer.Count}";

                int progressPercent = (int)(totalGames / (double)totalGamesToPlay * 100);
                toolStripStatusLabel1.Text = $"Running: {totalGames}/{totalGamesToPlay} games completed ({progressPercent}%)";
            }
        });
    }

    private void UpdatePercentChart(int deepLearningThreshold, bool isBetter)
    {
        if (_redPercentHistory.Count == 0)
        {
            return;
        }

        Invoke(() =>
        {
            winPercentChart.ClearData();
            winPercentChart.DeepLearnThreshold = deepLearningThreshold;
            winPercentChart.PositionsRedNetworkBetter.Add(isBetter);

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

        var sharedTrainingBuffer = new TrainingBuffer();
        var tasks = new List<Task>();
        var globalStats = new ConcurrentDictionary<int, (int redWins, int yellowWins, int drawWins, int totalWins)>();
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
                int gamesPlayed = 0;
                int redWins = 0;
                int yellowWins = 0;
                int draws = 0;

                GamePanel panel = _gamePanels[index];
                CompactConnect4Game game = panel.Game;
                PictureBox pictureBox = panel.PictureBox;

                var redMcts = new Mcts(mcstIterations, mctsRed.ValueNetwork, mctsRed.PolicyNetwork);
                var yellowMcts = new Mcts(mcstIterations, mctsYellow.ValueNetwork, mctsYellow.PolicyNetwork);

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

                            redMcts.SetWinnerTrainingBuffer(Winner.Draw);
                            yellowMcts.SetWinnerTrainingBuffer(Winner.Draw);

                            _ = BeginInvoke(() =>
                            {
                                gameCount++;
                                Text = $"Games played {gameCount}/{totalGames} Data: {_trainingBuffer.Count} New: {_trainingBuffer.NewEntries} Replay: {_replayBuffer.Count}";

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
                            redMcts.SetWinnerTrainingBuffer((Winner)winner);
                            yellowMcts.SetWinnerTrainingBuffer((Winner)winner);

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
                                Text = $"Games played {gameCount}/{totalGames} Data: {_trainingBuffer.Count} New: {_trainingBuffer.NewEntries} Replay: {_replayBuffer.Count}";

                                panel.RecordResult((Winner)winner);
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

                lock (sharedTrainingBuffer)
                {
                    _ = BeginInvoke(() => Text = $"Games played {gameCount}/{totalGames} Data: {_trainingBuffer.Count} New: {_trainingBuffer.NewEntries} Replay: {_replayBuffer.Count}");

                    _trainingBuffer.MergeFrom(redMcts.GetTrainingBuffer());
                    _trainingBuffer.MergeFrom(yellowMcts.GetTrainingBuffer());
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
        foreach ((int redWins, int yellowWins, int drawWins, int totalWins) in globalStats.Values)
        {
            red += redWins;
            yellow += yellowWins;
            draw += drawWins;
            total += totalWins;
        }

        return (red, yellow, draw, total);
    }
}