using Connect4.Ais;
using Connect4.GameParts;
using DeepNetwork;
using DeepNetwork.NetworkIO;
using System.Collections.Concurrent;
using System.Diagnostics;

namespace Connect4;

public partial class Form1 : Form
{
    private const int ArenaIterations = 1000;
    private const int ChampionsToPlay = 6;
    private const int DeepLearningThreshold = 55;
    private const int DeepLearningThresholdMin = 35;
    private const double ErrorConfidence = 1.95;
    private const double ExplorationConstant = 1.28;
    private const int MctsIterations = 400;
    private const int SelfPlayGames = 1000;
    private const int Patience = 7;
    private const int MaxPatienceLevel = 2;
    private const string Unknown = "Random";
    private const int VsGames = 500;
    private readonly AgentCatalog _agentCatalog;
    private readonly List<double> _drawPercentHistory = [];
    private readonly List<double> _redPercentHistory = [];
    private readonly TelemetryHistory _telemetryHistory = new();
    private readonly List<double> _yellowPercentHistory = [];
    private readonly TeacherQueue<Agent> _teacherQueue = new();
    private const int MaxTeachingSessions = 6;
    private const int TrainingSteps = 100;
    private double _drawPercent;
    private double _redPercent;
    private double _redWithDrawPercent;
    private Agent? _teacherAgent;
    private double _yellowPercent;
    private double _yellowWithDrawPercent;

    public async Task BattleArena()
    {
        ResetChart();

        bool skipTraining = false;
        int i = 0;
        Mcts? trainedAgent = null;
        Mcts? nextAgent = null;

        List<Agent> championAgents = _agentCatalog.GetLatestAgents(ChampionsToPlay);
        if (championAgents.Count == 0)
        {
            Agent agent = CreateAgent(ExplorationConstant, _yellowMcts, _teacherAgent);
            _agentCatalog.Add(agent);
            championAgents.Add(agent);
        }

        _teacherAgent = championAgents.First();

        int ChampionsRemaining = Math.Min(championAgents.Count, ChampionsToPlay);

        while (i < ArenaIterations && !_arenaCancelationSource.IsCancellationRequested)
        {
            i++;
            if (!skipTraining)
            {
                _telemetryHistory.BeginAddingNewEntries();

                var stopwatch = Stopwatch.StartNew();
                await SelfPlayParallel(_teacherAgent, SelfPlayGames);
                stopwatch.Stop();

                if (_arenaCancelationSource.IsCancellationRequested)
                {
                    _ = BeginInvoke(() =>
                    {
                        textBox3.AddLine("Battle Arena cancelled.");
                    });

                    return;
                }

                stopwatch = Stopwatch.StartNew();
                if (trainedAgent is not null && !ReferenceEquals(trainedAgent, _redMcts))
                {
                    try
                    {
                        trainedAgent.Dispose();
                    }
                    catch { }

                    trainedAgent = null;
                }

                trainedAgent = nextAgent ?? new Mcts(MctsIterations, _redMcts.ValueNetwork!.Clone(), _redMcts.PolicyNetwork!.Clone());
                _ = await TrainAsync(trainedAgent);
                stopwatch.Stop();

                _ = BeginInvoke(() =>
                {
                    textBox3.AddLine($"Training on Selfplay done in {stopwatch.ElapsedMilliseconds} ms");
                });
            }

            skipTraining = false;

            _telemetryHistory.BeginAddingNewEntries();

            var stopwatch2 = Stopwatch.StartNew();
            bool isBetter = await EvaluateAgent(trainedAgent);
            stopwatch2.Stop();

            _ = BeginInvoke(() =>
            {
                textBox3.AddLine($"Evaluation done in {stopwatch2.ElapsedMilliseconds} ms");
            });

            if (isBetter)
            {
                if (ChampionsRemaining <= 1)
                {
                    if (trainedAgent is not null && !ReferenceEquals(_redMcts, trainedAgent))
                    {
                        try
                        {
                            _redMcts?.Dispose();
                        }
                        catch { }
                    }

                    _redMcts = trainedAgent ?? _redMcts;

                    _agentCatalog.Add(CreateAgent(ExplorationConstant, _redMcts, championAgents.First()));
                    championAgents = _agentCatalog.GetLatestAgents(ChampionsToPlay);
                    _teacherAgent = championAgents.First();

                    _yellowMcts = new Mcts(MctsIterations, _teacherAgent.ValueNetwork?.Clone(), _teacherAgent.PolicyNetwork?.Clone());

                    _telemetryHistory.ClearAll();

                    ChampionsRemaining = championAgents.Count;

                    _ = BeginInvoke(() =>
                    {
                        textBox3.AddLine("Boss Dead: Yellow has new network");
                    });
                }
                else
                {
                    skipTraining = true;
                    ChampionsRemaining--;

                    _teacherAgent = championAgents[^ChampionsRemaining];

                    _ = BeginInvoke(() =>
                    {
                        textBox3.AddLine($"Boss Lives {ChampionsRemaining}: Reduced boss life skipping training");
                    });
                }
            }
            else
            {
                _teacherAgent = championAgents.First();
                ChampionsRemaining = championAgents.Count;

                _ = BeginInvoke(() =>
                {
                    textBox3.AddLine($"Boss Lives {ChampionsRemaining}: boss unfased need more training");
                });
            }
        }
    }

    /// <summary>
    /// Only evaluates against the Random Mcts
    /// </summary>
    public async Task BattleArenaAlternate()
    {
        ResetChart();

        int i = 0;
        double previousImprovementGame1 = 0;
        double previousImprovementGame2 = 0;
        double previousBestGame1 = 0;
        double previousBestGame2 = 0;

        int totalTeachers = 0;

        if (_agentCatalog.Entries.Count == 0)
        {
            var randomMcts = new Mcts(MctsIterations, _oldValueNetwork.Clone(), _oldPolicyNetwork.Clone());
            _agentCatalog.Add(CreateAgent(ExplorationConstant, randomMcts, null));
        }

        Agent strongestAgent = _agentCatalog.GetLatestAgents(1).First();
        Agent challengerAgent = strongestAgent.Clone();

        Mcts trainedMcts = challengerAgent.ToMctsCloned(MctsIterations);
        trainedMcts.PolicyNetwork!.Trained = true;
        trainedMcts.ValueNetwork!.Trained = true;

        List<Agent> champions = [.. _agentCatalog.Entries.Values];
        int championToPlayAgainst = champions.Count;

        foreach (Agent agents in _agentCatalog.Entries.Values)
        {
            if (agents.TeachingSessions < MaxTeachingSessions)
            {
                _teacherQueue.Enqueue(agents.Clone());
            }
        }

        _teacherAgent = _teacherQueue.Dequeue()!.Clone();

        _yellowMcts = champions[0].ToMctsCloned(MctsIterations);

        while (i < ArenaIterations && !_arenaCancelationSource.IsCancellationRequested)
        {
            i++;
            championToPlayAgainst--;

            // 1 Evaluate the trained network
            textBox2.AddLine($" === Challenger {challengerAgent.LatestWinRate:f0}% " +
                $"vs Champion {champions.Count - championToPlayAgainst} / {champions.Count} ===");
            var stopwatch2 = Stopwatch.StartNew();

            _telemetryHistory.BeginAddingNewEntries();

            Agent trainedAgent = CreateAgent(ExplorationConstant, trainedMcts, challengerAgent);
            (bool isImproved, bool isBetter, double currentGame1, double currentGame2) = await EvaluateAgentAlternate(
                trainedAgent,
                previousImprovementGame1,
                previousImprovementGame2,
                previousBestGame1,
                previousBestGame2,
                true,
                VsGames);
            stopwatch2.Stop();

            if (isBetter)
            {
                previousImprovementGame1 = 0;
                previousImprovementGame2 = 0;
                previousBestGame1 = currentGame1;
                previousBestGame2 = currentGame2;

                challengerAgent = CreateAgent(ExplorationConstant, trainedMcts!, challengerAgent);
                challengerAgent.LatestWinRate = (currentGame1 + currentGame2) / 2;

                _teacherAgent!.TeachingSessions = 0;
                _teacherAgent.LatestWinRate = _teacherAgent.LatestWinRate == 0
                    ? challengerAgent.LatestWinRate
                    : _teacherAgent.LatestWinRate;

                Agent betterAgent = challengerAgent.Clone();
                _teacherQueue.Enqueue(betterAgent);
                totalTeachers++;

                _agentCatalog.Add(betterAgent);
                _agentCatalog.SaveCatalog();

                _telemetryHistory.ClearAll();

                champions = [.. _agentCatalog.Entries.Values];

                textBox2.AddLine($"Better added as Teacher. Teacher total: {_teacherQueue.Count}");
            }
            else if (isImproved)
            {
                previousImprovementGame1 = currentGame1;
                previousImprovementGame2 = currentGame2;

                _teacherAgent!.TeachingSessions = 0;

                challengerAgent = CreateAgent(ExplorationConstant, trainedMcts!, challengerAgent);

                textBox2.AddLine($"Improved. Teacher total: {_teacherQueue.Count}");
            }
            else
            {
                _teacherAgent!.TeachingSessions++;

                textBox2.AddLine($"No improvement session {_teacherAgent.TeachingSessions}. Teacher total: {_teacherQueue.Count}");
            }

            textBox2.AddLine($"Evaluation done in {stopwatch2.Elapsed:hh\\:mm\\:ss}");

            if (_teacherAgent.TeachingSessions >= MaxTeachingSessions)
            {
                if (_teacherQueue.Count == 0)
                {
                    textBox2.AddLine($"Training DONE no new teachers after teacher {_teacherAgent?.Generation}");
                    break;
                }

                previousImprovementGame1 = 0;
                previousImprovementGame2 = 0;
                _telemetryHistory.ClearAll();
                challengerAgent = _teacherQueue.ToArray().Last().Clone();

                _teacherAgent.Dispose();
                _teacherAgent = _teacherQueue.Dequeue()!.Clone();

                textBox2.AddLine($"New teacher {_teacherAgent!.Generation} Teacher total: {_teacherQueue.Count}");
            }

            championToPlayAgainst = champions.Count;

            // 2 play a minimum amont of self play games to refresh the telemetry history
            var stopwatch = Stopwatch.StartNew();
            _telemetryHistory.BeginAddingNewEntries();

            await SelfPlayParallel(_teacherAgent, SelfPlayGames);
            stopwatch.Stop();
            textBox2.AddLine($"Selfplay done in {stopwatch.Elapsed:hh\\:mm\\:ss}");

            if (_arenaCancelationSource.IsCancellationRequested)
            {
                textBox2.AddLine("Battle Arena cancelled.");

                return;
            }

            // 3 Train a new red network
            var stopwatch3 = Stopwatch.StartNew();

            trainedMcts?.Dispose();
            trainedMcts = challengerAgent.ToMctsCloned(MctsIterations);
            _ = await TrainAsync(trainedMcts);
            stopwatch3.Stop();

            textBox2.AddLine($"Training done in {stopwatch3.Elapsed:hh\\:mm\\:ss}");
        }
    }

    private static Agent CreateAgent(double explorationFactor, Mcts mcts, Agent? previousAgent)
    {
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
            Generation = previousAgent?.Generation + 1 ?? 0,
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
        textBox2.AddLine(string.Empty);
        textBox2.AddLine($"Playing Challenger Vs Champ {_teacherAgent?.Generation}");

        (_, _, int draw, int total) = await VsPlayParallel(trainedRedMcts!, _yellowMcts, MctsIterations, ExplorationConstant, isChalengerRed: true);
        double agent1Game1 = _redWithDrawPercent;
        double agent2Game1 = _yellowWithDrawPercent;
        int draws = draw;
        int totalGames = total;

        textBox2.AddLine($"Challenger {_redWithDrawPercent}% Champ {_yellowWithDrawPercent}%");

        // Now Evaluate the current agent against the trained network
        textBox2.AddLine($"Playing Champ {_teacherAgent?.Generation} Vs Challenger");

        (_, _, draw, total) = await VsPlayParallel(_yellowMcts, trainedRedMcts!, MctsIterations, ExplorationConstant, isChalengerRed: false);
        double agent1Game2 = _yellowWithDrawPercent;
        double agent2Game2 = _redWithDrawPercent;
        draws += draw;
        totalGames += total;

        textBox2.AddLine($"Challenger {_yellowWithDrawPercent}% Champ {_redWithDrawPercent}%");

        //Draw the chart
        double redPercentAfterTraining = Math.Min(agent1Game2, agent1Game1);
        double yellowPercentAfterTraining = Math.Min(agent2Game2, agent2Game1);
        _redPercentHistory.Add(redPercentAfterTraining);
        _yellowPercentHistory.Add(yellowPercentAfterTraining);
        _drawPercentHistory.Add(Math.Round(draws / (double)totalGames * 100, 2));

        textBox2.AddLine($"Final Result Challenger {redPercentAfterTraining:f2}% Champ {yellowPercentAfterTraining:f2}%");

        // Check if the new agent wins more than threshold of the games on one side and is better than the current agent on the other side
        double agent1Max = Math.Max(agent1Game1, agent1Game2) / 100;
        double agent1Min = Math.Min(agent1Game1, agent1Game2) / 100;
        double agent2Min = Math.Min(agent2Game1, agent2Game2) / 100;

        double marginOfError = ErrorConfidence * Math.Sqrt(agent1Min * (1 - agent1Min) / VsGames);
        bool isBetter = (agent1Max * 100) > DeepLearningThreshold
            && agent2Min + marginOfError < agent1Min
            && (agent1Min * 100) > DeepLearningThresholdMin;

        TrainingProgress progress = isBetter
            ? TrainingProgress.IsBetter
            : TrainingProgress.IsFailed;

        UpdatePercentChart(DeepLearningThreshold, progress);

        return isBetter;
    }

    private async Task<(bool isImproved, bool isBetter, double currentGame1, double currentGame2)> EvaluateAgentAlternate(
        Agent trainedAgent,
        double improvementGame1,
        double improvementGame2,
        double bestGame1,
        double bestGame2,
        bool isFinalEvaluation,
        int vsGames)
    {
        var trainedMcts = trainedAgent.ToMcts(MctsIterations);

        // Evaluate the trained network against the current agent
        textBox2.AddLine(string.Empty);
        textBox2.AddLine($"Playing Challenger {trainedAgent.Generation} Vs Champ");

        (_, _, _, _) = await VsPlayParallel(
            trainedMcts,
            _yellowMcts,
            MctsIterations,
            ExplorationConstant,
            isChalengerRed: true,
            vsGames);

        double agent1Game1 = _redWithDrawPercent;
        double agent2Game1 = _yellowWithDrawPercent;

        // Now Evaluate the current agent against the trained network
        textBox2.AddLine($"Challenger {_redWithDrawPercent}% Champ {_yellowWithDrawPercent}%");
        textBox2.AddLine($"Playing Champ Vs Challenger {trainedAgent?.Generation}");

        (_, _, _, _) = await VsPlayParallel(
            _yellowMcts,
            trainedMcts,
            MctsIterations,
            ExplorationConstant,
            isChalengerRed: false,
            vsGames);

        double agent1Game2 = _yellowWithDrawPercent;
        double agent2Game2 = _redWithDrawPercent;

        textBox2.AddLine($"Challenger {_yellowWithDrawPercent}% Champ {_redWithDrawPercent}%");

        DrawAverageChartLines(improvementGame1, improvementGame2, agent1Game1, agent2Game1, agent1Game2, agent2Game2);

        (bool isImproved, bool isBetter) = CheckVsAverageScore(
            improvementGame1, improvementGame2, bestGame1, bestGame2, agent1Game1, agent2Game1, agent1Game2, agent2Game2);

        TrainingProgress progress = isBetter && isFinalEvaluation
            ? TrainingProgress.IsBetterSignificantly
            : isBetter ? TrainingProgress.IsBetter : isImproved ? TrainingProgress.IsImproved : TrainingProgress.IsFailed;
        UpdatePercentChart(DeepLearningThreshold, progress);

        return (isImproved, isBetter, agent1Game1, agent1Game2);
    }

    private void DrawMinimumChartLines(
        double improvementGame1,
        double improvementGame2,
        double agent1Game1,
        double agent2Game1,
        double agent1Game2,
        double agent2Game2)
    {
        double redPercentAfterTraining = Math.Min(agent1Game2, agent1Game1);
        double yellowPercentAfterTraining = Math.Min(agent2Game2, agent2Game1);
        double drawPercentBestLine = Math.Min(improvementGame1, improvementGame2);

        AddToChartHistory(redPercentAfterTraining, yellowPercentAfterTraining, drawPercentBestLine);
    }

    private void DrawAverageChartLines(
        double improvementGame1,
        double improvementGame2,
        double agent1Game1,
        double agent2Game1,
        double agent1Game2,
        double agent2Game2)
    {
        double redPercentAfterTraining = (agent1Game2 + agent1Game1) / 2;
        double yellowPercentAfterTraining = (agent2Game2 + agent2Game1) / 2;
        double drawPercentBestLine = (improvementGame1 + improvementGame2) / 2;

        AddToChartHistory(redPercentAfterTraining, yellowPercentAfterTraining, drawPercentBestLine);
    }

    private void AddToChartHistory(double redPercentAfterTraining, double yellowPercentAfterTraining, double drawPercentBestLine)
    {
        _redPercentHistory.Add(redPercentAfterTraining);
        _yellowPercentHistory.Add(yellowPercentAfterTraining);
        _drawPercentHistory.Add(drawPercentBestLine);

        textBox2.AddLine($"Final Result Challenger {redPercentAfterTraining:f2}% Champ {yellowPercentAfterTraining:f2}%");
    }

    private (bool isImproved, bool isBetter) CheckVsAverageScore(
        double improvementGame1,
        double improvementGame2,
        double bestGame1,
        double bestGame2,
        double agent1Game1,
        double agent2Game1,
        double agent1Game2,
        double agent2Game2)
    {
        double agent1Avg = (agent1Game1 + agent1Game2) / 2 / 100;
        double agent2Avg = (agent2Game1 + agent2Game2) / 2 / 100;
        double improveAvg = (improvementGame1 + improvementGame2) / 2 / 100;
        double bestAvg = (bestGame1 + bestGame2) / 2 / 100;

        double marginOfError = 0;// ErrorConfidence * Math.Sqrt(agent1Avg * (1 - agent1Avg) / vsGames);
        bool isImproved = improveAvg < agent1Avg;
        bool isBetter = agent2Avg + marginOfError < agent1Avg
            && bestAvg + marginOfError < agent1Avg;

        textBox2.AddLine($"Improve: {improveAvg:f2} < {agent1Avg:f2} = {isImproved} ");
        textBox2.AddLine($"Better : {agent2Avg + marginOfError:f2} < {agent1Avg:f2} " +
            $"AND {bestAvg + marginOfError:f2} ({bestAvg:f2} + {marginOfError:f2})< {agent1Avg:f2} = {isBetter}");

        return (isImproved, isBetter);
    }

    private static (bool isImproved, bool isBetter) CheckVsMinimumScore(
        double improvementGame1,
        double improvementGame2,
        double betterGame1,
        double betterGame2,
        int vsGames,
        double agent1Game1,
        double agent2Game1,
        double agent1Game2,
        double agent2Game2)
    {
        double agent1Min = Math.Min(agent1Game1, agent1Game2) / 100;
        double agent2Min = Math.Min(agent2Game1, agent2Game2) / 100;
        double bestMin = Math.Min(improvementGame1, improvementGame2) / 100;

        double marginOfError = ErrorConfidence * Math.Sqrt(agent1Min * (1 - agent1Min) / vsGames);
        bool isImproved = bestMin + marginOfError < agent1Min;
        bool isBetter = agent2Min + 2 * marginOfError < agent1Min
            && (betterGame1 + betterGame2) / 2 < (agent1Game1 + agent1Game2) / 2
            && agent1Min >= 0.30;

        return (isImproved, isBetter);
    }

    private void ResetChart()
    {
        winPercentChart.Reset();
    }

    private async Task SelfPlayParallel(Agent agent, int numberOfGames)
    {
        CancellationToken cancellationToken = _arenaCancelationSource.Token;
        int processorCount = Environment.ProcessorCount;
        int parallelGames = Math.Max(2, processorCount * 2);

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

                var redMcts = new Mcts(MctsIterations, agent.ValueNetwork!.Clone(), agent.PolicyNetwork!.Clone());
                var yellowMcts = new Mcts(MctsIterations, agent.ValueNetwork.Clone(), agent.PolicyNetwork.Clone());

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
                                Text = $"{gameCount}/{totalGames} Data: {_telemetryHistory.Count} New: {_telemetryHistory.NewEntries}";

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
                            redMcts.SetWinnerTelemetryHistory((Winner)winner);
                            yellowMcts.SetWinnerTelemetryHistory((Winner)winner);

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
                                Text = $"{gameCount}/{totalGames} Data: {_telemetryHistory.Count} New: {_telemetryHistory.NewEntries}";

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

                lock (sharedTelemetryHistory)
                {
                    _ = BeginInvoke(() => Text = $"{gameCount}/{totalGames} Data: {_telemetryHistory.Count} New: {_telemetryHistory.NewEntries}");

                    _telemetryHistory.MergeFrom(redMcts.GetTelemetryHistory());
                    _telemetryHistory.MergeFrom(yellowMcts.GetTelemetryHistory());
                }
            }));
        }

        await Task.WhenAll(tasks).ConfigureAwait(false);
    }

    private Task<(int runs, double error)> TrainAsync(Mcts mcts)
    {
        _ = Invoke(() => textBox3.Text = string.Empty);

        TelemetryHistory telemetryHistory = _telemetryHistory;
        int minPolicRuns = int.MaxValue;
        double minPolicyError = double.MaxValue;

        INetworkTrainer valueTrainer = NetworkTrainerFactory.CreateNetworkTrainer(mcts.ValueNetwork);
        INetworkTrainer policyTrainer = NetworkTrainerFactory.CreateNetworkTrainer(mcts.PolicyNetwork);

        double previousValueError = double.MaxValue;
        double previousPolicyError = double.MaxValue;
        double valueError = 0;
        double policyError = 0;
        double clonedAtValueError = 0;
        double clonedAtPolicyError = 0;
        bool valueStopEarly = false;
        bool policyStopEarly = false;
        IStandardNetwork? tempValueNetwork = mcts.ValueNetwork!.Clone();
        tempValueNetwork.Trained = true;
        IStandardNetwork? tempPolicyNetwork = mcts.PolicyNetwork!.Clone();
        tempPolicyNetwork.Trained = true;

        int valuePatienceCounter = 0;
        int policyPatienceCounter = 0;
        double bestValueValidation = double.MaxValue;
        double bestPolicyValidation = double.MaxValue;

        int steps = TrainingSteps;
        int i = -1;
        string vStop = string.Empty;
        string pStop = string.Empty;

        (double[][] fullTrainingData, double[][] fullPolicyExpectedData, double[][] fullValueExpectedData) = telemetryHistory
            .GetTrainingDataRandomAveragedNewFirst(_telemetryHistory.Count);

        // Trin on 80% and validate with 20%
        int trainCount = (int)(fullTrainingData.Length * 0.8);
        double[][] trainingData = [.. fullTrainingData.Take(trainCount)];
        double[][] policyExpectedData = [.. fullPolicyExpectedData.Take(trainCount)];
        double[][] valueExpectedData = [.. fullValueExpectedData.Take(trainCount)];

        double[][] validationData = [.. fullTrainingData.Skip(trainCount)];
        double[][] validationPolicyData = [.. fullPolicyExpectedData.Skip(trainCount)];
        double[][] validationValueData = [.. fullValueExpectedData.Skip(trainCount)];

        double valueValidationError = double.MaxValue;
        double policyValidationError = double.MaxValue;
        int valuePatienceLevel = 1;
        int policyPatienceLevel = 1;
        int clonedAtValueStep = 0;
        int clonedAtPolicyStep = 0;

        while (i < steps)
        {
            i++;

            if (!valueStopEarly)
            {
                double learningRate = GetValueLearningRate(valuePatienceLevel);
                valueError = valueTrainer.Train(trainingData, valueExpectedData, learningRate);
                valueValidationError = ComputeValidationError(mcts.ValueNetwork, validationData, validationValueData, isValue: true);
                valueValidationError = Math.Round(valueValidationError, 8);
            }

            if (!policyStopEarly)
            {
                double learningRate = GetPolicyLearningRate(policyPatienceLevel);
                policyError = policyTrainer.Train(trainingData, policyExpectedData, learningRate);
                policyValidationError = ComputeValidationError(mcts.PolicyNetwork, validationData, validationPolicyData, isValue: false);
                policyValidationError = Math.Round(policyValidationError, 8);
            }

            string valueArrow = valueError >= previousValueError ? "🡹" : "🡻";
            string policyArrow = policyError >= previousPolicyError ? "🡹" : "🡻";
            string valValueArrow = valueValidationError >= bestValueValidation ? "🡹" : "🡻";
            string valPolicyArrow = policyValidationError >= bestPolicyValidation ? "🡹" : "🡻";

            Invoke(() =>
            {
                textBox3.AddLine($"Step {i}\t V: {Math.Round(valueValidationError, 8):F8} {valValueArrow} P:{valuePatienceCounter}/{Patience} \t " +
                    $"P: {Math.Round(policyValidationError, 8):F8} {valPolicyArrow} P:{policyPatienceCounter}/{Patience}");
            });

            // Early stop on Value
            if (valueValidationError < bestValueValidation)
            {
                bestValueValidation = valueValidationError;
                valuePatienceCounter = 0;
                if (!valueStopEarly)
                {
                    tempValueNetwork = mcts.ValueNetwork!.Clone();
                    clonedAtValueError = valueValidationError;
                    clonedAtValueStep = i;
                }
            }
            else if (!valueStopEarly)
            {
                valuePatienceCounter++;
            }

            // Early stop on Policy
            if (policyValidationError < bestPolicyValidation)
            {
                bestPolicyValidation = policyValidationError;
                policyPatienceCounter = 0;
                if (!policyStopEarly)
                {
                    tempPolicyNetwork = mcts.PolicyNetwork!.Clone();
                    clonedAtPolicyError = policyValidationError;
                    clonedAtPolicyStep = i;
                }
            }
            else if (!policyStopEarly)
            {
                policyPatienceCounter++;
            }

            if (!valueStopEarly && valuePatienceCounter >= Patience)
            {
                if (valuePatienceLevel < MaxPatienceLevel)
                {
                    //start agains with the best network so far using a lower learning rate
                    mcts.ValueNetwork = tempValueNetwork.Clone();
                    valueTrainer = NetworkTrainerFactory.CreateNetworkTrainer(mcts.ValueNetwork);

                    double oldLearningRate = GetValueLearningRate(valuePatienceLevel);
                    valuePatienceLevel++;
                    valuePatienceCounter = 0;
                    double newLearningRate = GetValueLearningRate(valuePatienceLevel);
                    textBox3.AddLine($"V: learn rate {oldLearningRate} -> {newLearningRate}");
                }
                else
                {
                    Invoke(() =>
                    {
                        vStop = $"V cloned at step {clonedAtValueStep}/{i}. Best val error: {Math.Round(bestValueValidation, 8):F8}";
                        textBox3.AddLine(vStop);
                    });
                    valueStopEarly = true;
                }
            }

            if (!policyStopEarly && policyPatienceCounter >= Patience)
            {
                if (policyPatienceLevel < MaxPatienceLevel)
                {
                    //start agains with the best network so far using a lower learning rate
                    mcts.PolicyNetwork = tempPolicyNetwork.Clone();
                    policyTrainer = NetworkTrainerFactory.CreateNetworkTrainer(mcts.PolicyNetwork);

                    double oldLearningRate = GetPolicyLearningRate(policyPatienceLevel);
                    policyPatienceLevel++;
                    policyPatienceCounter = 0;
                    double newLearningRate = GetPolicyLearningRate(policyPatienceLevel);
                    textBox3.AddLine($"\t \t \t \t P: learn rate {oldLearningRate} -> {newLearningRate}");
                }
                else
                {
                    Invoke(() =>
                    {
                        pStop = $"P stop at step {clonedAtPolicyStep}/{i}. Best val error: {Math.Round(bestPolicyValidation, 8):F8}";
                        textBox3.AddLine(pStop);
                    });
                    policyStopEarly = true;
                }
            }

            if (valueStopEarly && policyStopEarly)
            {
                Invoke(() =>
                {
                    textBox3.AddLine($"Both networks stopped early at step {i}");
                });
                break;
            }

            previousValueError = valueError;
            previousPolicyError = policyError;
        }

        mcts.PolicyNetwork = tempPolicyNetwork;
        mcts.ValueNetwork = tempValueNetwork;

        string y = $"Current agent is {_teacherAgent?.Id ?? "None"} generation: {_teacherAgent?.Generation ?? 0}";
        string clonedErrors = $"Best val errors - V:{Math.Round(clonedAtValueError, 8):F8} P:{Math.Round(clonedAtPolicyError, 8):F8}";
        Invoke(() =>
        {
            textBox3.AddLine(string.Empty);
            textBox3.AddLine(clonedErrors);
            textBox3.AddLine(vStop);
            textBox3.AddLine(pStop);
            textBox3.AddLine($"Max steps {steps}, completed {i + 1} steps");
            textBox3.AddLine($"Training set = {trainingData.Length}, Validation set = {validationData.Length}");
            textBox3.AddLine($"Patience = {Patience}");
            textBox3.AddLine($"Total boardstates trained {MiniBatchNetworkTrainer.BatchSize * (i + 1)}");
            textBox3.AddLine(y);
        });

        return Task.FromResult<(int, double)>((minPolicRuns, minPolicyError));
    }

    private static double GetValueLearningRate(int patienceLevel)
    {
        double[] valueLearningRates = [0.001, 0.0001, 0.00008];

        return valueLearningRates[Math.Min(patienceLevel - 1, valueLearningRates.Length - 1)];
    }

    private static double GetPolicyLearningRate(int patienceLevel)
    {
        double[] valueLearningRates = [0.001, 0.0001, 0.00001];

        return valueLearningRates[Math.Min(patienceLevel - 1, valueLearningRates.Length - 1)];
    }

    private static double ComputeValidationError(IStandardNetwork network, double[][] inputs, double[][] targets, bool isValue)
    {
        double totalError = 0;
        int count = inputs.Length;

        for (int i = 0; i < count; i++)
        {
            double[] output = network.Calculate(inputs[i]);

            if (isValue)
            {
                double diff = output[0] - targets[i][0];
                totalError += 0.5 * diff * diff;
            }
            else
            {
                for (int j = 0; j < output.Length; j++)
                {
                    double p = Math.Max(targets[i][j], 1e-11);
                    double q = Math.Max(output[j], 1e-11);
                    totalError += -p * Math.Log(q);
                }
            }
        }

        return totalError / count;
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
                    $"R: {_redWithDrawPercent:F0}%" +
                    $"Y: {_yellowWithDrawPercent:F0}%" +
                    $"D: {_drawPercent:F0}% " +
                    $"Data: {_telemetryHistory.Count}";

                int progressPercent = (int)(totalGames / (double)totalGamesToPlay * 100);
                toolStripStatusLabel1.Text = $"Running: {totalGames}/{totalGamesToPlay} games completed ({progressPercent}%)";
            }
        });
    }

    private void UpdatePercentChart(int deepLearningThreshold, TrainingProgress progress)
    {
        if (_redPercentHistory.Count == 0)
        {
            return;
        }

        Color dotColor = progress switch
        {
            TrainingProgress.IsBetterSignificantly => Color.FromArgb(164, 83, 255),
            TrainingProgress.IsBetter => Color.FromArgb(0, 255, 0),
            TrainingProgress.IsImproved => Color.FromArgb(200, 200, 200),
            TrainingProgress.IsFailed => Color.FromArgb(255, 0, 0),
            _ => Color.Black,
        };

        Invoke(() =>
        {
            winPercentChart.ClearData();
            winPercentChart.DeepLearnThreshold = deepLearningThreshold;
            winPercentChart.PositionsRedNetworkBetter.Add(dotColor);

            for (int i = 0; i < _redPercentHistory.Count; i++)
            {
                winPercentChart.AddDataPoint(_redPercentHistory[i], _yellowPercentHistory[i], _drawPercentHistory[i]);
            }
        });
    }

    private async Task<(int redWins, int yellowWins, int drawWins, int totalWins)>
        VsPlayParallel(
            Mcts mctsRed,
            Mcts mctsYellow,
            int mcstIterations,
            double explorationFactor,
            bool isChalengerRed,
            int? vsGames = null)
    {
        CancellationToken cancellationToken = _arenaCancelationSource.Token;

        int processorCount = Environment.ProcessorCount;
        int parallelGames = Math.Max(2, processorCount * 2);

        int totalGames = vsGames ?? VsGames;

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
        int gameCount = 0;

        for (int gameIndex = 0; gameIndex < parallelGames; gameIndex++)
        {
            int index = gameIndex;
            globalStats[index] = (0, 0, 0, 0);

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

                var redMcts = new Mcts(mcstIterations, mctsRed.ValueNetwork?.Clone(), mctsRed.PolicyNetwork?.Clone());
                var yellowMcts = new Mcts(mcstIterations, mctsYellow.ValueNetwork?.Clone(), mctsYellow.PolicyNetwork?.Clone());

                while (!cancellationToken.IsCancellationRequested && gamesPlayed < gamesToPlay)
                {
                    bool gameEnded = false;
                    int winner = 0;

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

                            redMcts.SetWinnerTelemetryHistory(Winner.Draw);
                            yellowMcts.SetWinnerTelemetryHistory(Winner.Draw);

                            _ = BeginInvoke(() =>
                            {
                                gameCount++;

                                panel.RecordResult(Winner.Draw);
                                pictureBox.Refresh();
                            });

                            game.ResetGame();
                            gameEnded = true;

                            globalStats[index] = (redWins, yellowWins, draws, gamesPlayed);
                            _ = BeginInvoke(() => UpdateGlobalStats(globalStats, totalGames));

                            continue;
                        }

                        winner = game.PlacePieceColumn(move);
                        _ = BeginInvoke(() => pictureBox.Refresh());

                        if (winner != 0)
                        {
                            redMcts.SetWinnerTelemetryHistory((Winner)winner);
                            yellowMcts.SetWinnerTelemetryHistory((Winner)winner);

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

                    lock (sharedTelemetryHistory)
                    {
                        if ((Winner)winner == Winner.Red && !isChalengerRed)
                        {
                            _telemetryHistory.MergeFrom(redMcts.GetTelemetryHistory());
                        }

                        if ((Winner)winner == Winner.Yellow && isChalengerRed)
                        {
                            _telemetryHistory.MergeFrom(yellowMcts.GetTelemetryHistory());
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