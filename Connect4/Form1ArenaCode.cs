using Connect4.Ais;
using Connect4.GameParts;
using DeepNetwork;
using DeepNetwork.NetworkIO;
using System.CodeDom;
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
    private const double ExplorationConstant = 1.68;
    private const int MctsIterations = 400;
    private const int MovingAverageSize = 2;
    private const int SelfPlayGames = 200;
    private const string Unknown = "Random";
    private const int VsGames = 500;
    private readonly AgentCatalog _agentCatalog;
    private readonly List<double> _drawPercentHistory = [];
    private readonly List<double> _redPercentHistory = [];
    private readonly TelemetryHistory _telemetryHistory = new();
    private readonly List<double> _yellowPercentHistory = [];
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

        // Get the current best agent from the catalog or create a new one if none exists
        List<Agent> championAgents = _agentCatalog.GetLatestAgents(ChampionsToPlay);
        if (championAgents.Count == 0)
        {
            _teacherAgent = CreateAgent(ExplorationConstant, _yellowMcts, _teacherAgent);
            _agentCatalog.Add(_teacherAgent);
            championAgents.Add(_teacherAgent);
        }
        else
        {
            _teacherAgent = championAgents.First();
        }

        int ChampionsRemaining = Math.Min(championAgents.Count, ChampionsToPlay);

        while (i < ArenaIterations && !_arenaCancelationSource.IsCancellationRequested)
        {
            i++;
            if (!skipTraining)
            {
                // Reset New entry count telemetry history
                _telemetryHistory.BeginAddingNewEntries();

                // play a minimum amont of self play games to refresh the telemetry history
                var stopwatch = Stopwatch.StartNew();
                await SelfPlayParallel(_teacherAgent, SelfPlayGames);
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
                trainedAgent = nextAgent ?? new Mcts(MctsIterations, _redMcts.ValueNetwork!.Clone(), _redMcts.PolicyNetwork!.Clone());
                _ = await TrainAsync(trainedAgent);
                stopwatch.Stop();

                _ = BeginInvoke(() =>
                {
                    _ = listBox1.Items.Add($"Training on Selfplay done in {stopwatch.ElapsedMilliseconds} ms");
                    listBox1.TopIndex = listBox1.Items.Count - 1;
                });
            }

            skipTraining = false;

            // Reset New entry count telemetry history
            _telemetryHistory.BeginAddingNewEntries();

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
                if (ChampionsRemaining <= 1)
                {
                    _redMcts = trainedAgent ?? _redMcts;

                    _agentCatalog.Add(CreateAgent(ExplorationConstant, _redMcts, championAgents.First()));
                    championAgents = _agentCatalog.GetLatestAgents(ChampionsToPlay);
                    _teacherAgent = championAgents.First();

                    _yellowMcts = new Mcts(MctsIterations, _teacherAgent.ValueNetwork?.Clone(), _teacherAgent.PolicyNetwork?.Clone());

                    _telemetryHistory.ClearAll();

                    ChampionsRemaining = championAgents.Count;

                    _ = BeginInvoke(() =>
                    {
                        _ = listBox1.Items.Add("Boss Dead: Yellow has new network");
                        listBox1.TopIndex = listBox1.Items.Count - 1;
                    });
                }
                else
                {
                    skipTraining = true;
                    ChampionsRemaining--;

                    _teacherAgent = championAgents[^ChampionsRemaining];

                    _ = BeginInvoke(() =>
                    {
                        _ = listBox1.Items.Add($"Boss Lives {ChampionsRemaining}: Reduced boss life skipping training");
                        listBox1.TopIndex = listBox1.Items.Count - 1;
                    });
                }
            }
            else
            {
                _teacherAgent = championAgents.First();
                ChampionsRemaining = championAgents.Count;

                _ = BeginInvoke(() =>
                {
                    _ = listBox1.Items.Add($"Boss Lives {ChampionsRemaining}: boss unfased need more training");
                    listBox1.TopIndex = listBox1.Items.Count - 1;
                });
            }
        }
    }

    /// <summary>
    /// This Arena plays only against the Random Mcts and not a Network version
    /// </summary>
    /// <returns></returns>
    public async Task BattleArenaAlternate()
    {
        ResetChart();

        int i = 0;
        double previousImprovementGame1 = 0;
        double previousImprovementGame2 = 0;

        double previousBetterGame1 = 0;
        double previousBetterGame2 = 0;

        int consecutiveLossFactor = 1;

        var randomMcts = new Mcts(MctsIterations, _oldValueNetwork.Clone(), _oldPolicyNetwork.Clone());

        if (_agentCatalog.Entries.Count == 0)
        {
            _agentCatalog.Add(CreateAgent(ExplorationConstant, randomMcts, null));
        }

        Agent strongestAgent = _agentCatalog.GetLatestAgents(1).First();
        Agent challengerAgent = strongestAgent.Clone();

        Mcts trainedMcts = challengerAgent.ToMctsCloned(MctsIterations);
        trainedMcts.PolicyNetwork!.Trained = true;
        trainedMcts.ValueNetwork!.Trained = true;

        List<Agent> champions = [.. _agentCatalog.Entries.Values];
        int championToPlayAgainst = champions.Count;

        _teacherAgent = strongestAgent.Clone();

        _yellowMcts = champions[0].ToMctsCloned(MctsIterations);

        while (i < ArenaIterations && !_arenaCancelationSource.IsCancellationRequested)
        {
            i++;
            championToPlayAgainst--;

            //_yellowMcts = champions[championToPlayAgainst].ToMctsCloned(MctsIterations);

            // 1 Evaluate the trained network
            AddToListBox($"Challenger vs Champion {champions.Count - championToPlayAgainst} / {champions.Count}");
            var stopwatch2 = Stopwatch.StartNew();
            _telemetryHistory.BeginAddingNewEntries();
            Agent trainedAgent = CreateAgent(ExplorationConstant, trainedMcts, challengerAgent);
            (bool isImproved, bool isBetter, double currentGame1, double currentGame2) = await EvaluateAgentAlternate(
                trainedAgent,
                previousImprovementGame1,
                previousImprovementGame2,
                previousBetterGame1,
                previousBetterGame2,
                true,
                VsGames);
            stopwatch2.Stop();
            AddToListBox($"Evaluation done in {stopwatch2.ElapsedMilliseconds} ms");

            //if (isBetter && championToPlayAgainst > 0)
            //{
            //    AddToListBox($"Challenger better than Champion {champions.Count - championToPlayAgainst} / {champions.Count}");
            //    continue;
            //}

            if (isBetter )
            {
                previousBetterGame1 = currentGame1;
                previousBetterGame2 = currentGame2;
                previousImprovementGame1 = 0;
                previousImprovementGame2 = 0;
                consecutiveLossFactor = 1;

                challengerAgent = CreateAgent(ExplorationConstant, trainedMcts!, challengerAgent);
                
                _teacherAgent = challengerAgent.Clone();
                _agentCatalog.Add(challengerAgent.Clone());

                champions = [.. _agentCatalog.Entries.Values];

                AddToListBox($"Challenger better changing Teacher");
            }
            else if (isImproved)
            {
                previousImprovementGame1 = currentGame1;
                previousImprovementGame2 = currentGame2;
                consecutiveLossFactor = 1;

                challengerAgent = CreateAgent(ExplorationConstant, trainedMcts!, challengerAgent);

                AddToListBox($"Challenger improved Upgrading");
            }
            else
            {
                consecutiveLossFactor = consecutiveLossFactor < 5 ? consecutiveLossFactor + 1 : consecutiveLossFactor;
                AddToListBox("Challenger not better try again");
            }
            
            championToPlayAgainst = champions.Count;

            // 2 play a minimum amont of self play games to refresh the telemetry history
            var stopwatch = Stopwatch.StartNew();
            _telemetryHistory.BeginAddingNewEntries();
            await SelfPlayParallel(_teacherAgent, SelfPlayGames * consecutiveLossFactor);
            stopwatch.Stop();

            if (_arenaCancelationSource.IsCancellationRequested)
            {
                AddToListBox("Battle Arena cancelled.");

                return;
            }

            // 3 Train a new red network
            var stopwatch3 = Stopwatch.StartNew();
            trainedMcts = challengerAgent.ToMctsCloned(MctsIterations);
            _ = await TrainAsync(trainedMcts);
            stopwatch3.Stop();

            AddToListBox($"Training on Selfplay done in {stopwatch3.ElapsedMilliseconds} ms");
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
            Generation = previousAgent?.Generation + 1 ?? 0,
            ExplorationFactor = explorationFactor
        };

        return currentAgent;
    }

    private void AddToListBox(string message)
    {
        _ = BeginInvoke(() =>
        {
            _ = listBox1.Items.Add(message);
            listBox1.TopIndex = listBox1.Items.Count - 1;
        });
    }

    /// <summary>
    /// Returns true if the trainedRedMcts is better than the current yellowMcts
    /// </summary>
    private async Task<bool> EvaluateAgent(Mcts? trainedRedMcts)
    {
        // Evaluate the trained network against the current agent
        AddToListBox(string.Empty);
        AddToListBox($"Playing Challenger Vs Champ {_teacherAgent?.Generation}");

        (_, _, int draw, int total) = await VsPlayParallel(trainedRedMcts!, _yellowMcts, MctsIterations, ExplorationConstant);
        double agent1Game1 = _redWithDrawPercent;
        double agent2Game1 = _yellowWithDrawPercent;
        int draws = draw;
        int totalGames = total;

        AddToListBox($"Challenger {_redWithDrawPercent}% Champ {_yellowWithDrawPercent}%");

        // Now Evaluate the current agent against the trained network
        AddToListBox($"Playing Champ {_teacherAgent?.Generation} Vs Challenger");

        (_, _, draw, total) = await VsPlayParallel(_yellowMcts, trainedRedMcts!, MctsIterations, ExplorationConstant);
        double agent1Game2 = _yellowWithDrawPercent;
        double agent2Game2 = _redWithDrawPercent;
        draws += draw;
        totalGames += total;

        AddToListBox($"Challenger {_yellowWithDrawPercent}% Champ {_redWithDrawPercent}%");

        //Draw the chart
        double redPercentAfterTraining = Math.Min(agent1Game2, agent1Game1);
        double yellowPercentAfterTraining = Math.Min(agent2Game2, agent2Game1);
        _redPercentHistory.Add(redPercentAfterTraining);
        _yellowPercentHistory.Add(yellowPercentAfterTraining);
        _drawPercentHistory.Add(Math.Round(draws / (double)totalGames * 100, 2));

        AddToListBox($"Final Result Challenger {redPercentAfterTraining}% Champ {yellowPercentAfterTraining}%");

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
        double betterGame1,
        double betterGame2,
        bool isFinalEvaluation,
        int vsGames)
    {
        // Evaluate the trained network against the current agent

        AddToListBox(string.Empty);
        AddToListBox($"Playing Challenger {trainedAgent?.Generation} Vs Champ");

        var trainedMcts = trainedAgent.ToMcts(MctsIterations);

        (_, _, _, _) = await VsPlayParallel(
            trainedMcts,
            _yellowMcts,
            MctsIterations,
            ExplorationConstant,
            vsGames);

        double agent1Game1 = _redWithDrawPercent;
        double agent2Game1 = _yellowWithDrawPercent;

        AddToListBox($"Challenger {_redWithDrawPercent}% Champ {_yellowWithDrawPercent}%");

        // Now Evaluate the current agent against the trained network

        AddToListBox($"Playing Champ Vs Challenger {trainedAgent?.Generation}");

        (_, _, _, _) = await VsPlayParallel(
            _yellowMcts,
            trainedMcts,
            MctsIterations,
            ExplorationConstant,
            vsGames);

        double agent1Game2 = _yellowWithDrawPercent;
        double agent2Game2 = _redWithDrawPercent;

        AddToListBox($"Challenger {_yellowWithDrawPercent}% Champ {_redWithDrawPercent}%");

        //Draw the chart
        double redPercentAfterTraining = Math.Min(agent1Game2, agent1Game1);
        double yellowPercentAfterTraining = Math.Min(agent2Game2, agent2Game1);
        double drawPercentBestLine = Math.Min(improvementGame1, improvementGame2);
        _redPercentHistory.Add(redPercentAfterTraining);
        _yellowPercentHistory.Add(yellowPercentAfterTraining);
        _drawPercentHistory.Add(drawPercentBestLine);

        AddToListBox($"Final Result Challenger {redPercentAfterTraining}% Champ {yellowPercentAfterTraining}%");

        // Check if the new agent better than the previous agent on the other side
        double agent1Min = Math.Min(agent1Game1, agent1Game2) / 100;
        double agent2Min = Math.Min(agent2Game1, agent2Game2) / 100;
        double bestMin = Math.Min(improvementGame1, improvementGame2) / 100;

        double marginOfError = ErrorConfidence * Math.Sqrt(agent1Min * (1 - agent1Min) / vsGames);
        bool isImproved = bestMin + marginOfError < agent1Min;
        bool isBetter = agent2Min + 2 * marginOfError < agent1Min
            && (betterGame1 + betterGame2) / 2 < (agent1Game1 + agent1Game2) / 2
            && agent1Min >= 0.30;

        TrainingProgress progress;
        if (isBetter && isFinalEvaluation)
        {
            progress = TrainingProgress.IsBetterSignificantly;
        }
        else if (isBetter)
        {
            progress = TrainingProgress.IsBetter;
        }
        else if(isImproved)
        {
            progress = TrainingProgress.IsImproved;
        }
        else
        {
            progress = TrainingProgress.IsFailed;
        }

        UpdatePercentChart(DeepLearningThreshold, progress);

        return (isImproved, isBetter, agent1Game1, agent1Game2);
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
        Invoke(listBox1.Items.Clear);

        TelemetryHistory telemetryHistory = _telemetryHistory;
        var random = new Random();
        int minPolicRuns = int.MaxValue;
        double minPolicyError = double.MaxValue;

        INetworkTrainer valueTrainer = NetworkTrainerFactory.CreateNetworkTrainer(mcts.ValueNetwork);
        INetworkTrainer policyTrainer = NetworkTrainerFactory.CreateNetworkTrainer(mcts.PolicyNetwork);

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
        IStandardNetwork? tempValueNetwork = mcts.ValueNetwork!.Clone();
        tempValueNetwork.Trained = true;
        IStandardNetwork? tempPolicyNetwork = mcts.PolicyNetwork!.Clone();
        tempPolicyNetwork.Trained = true;

        int movingAveragePolicy = MovingAverageSize;

        int steps =  _telemetryHistory.Count * 150;
            //Math.Max(1, sampleSize / MiniBatchNetworkTrainer.BatchSize);

        // moving average for value is larger because it fluctuates more
        int movingAverageValue = movingAveragePolicy;

        // Get all new entries plus a little bit of the old entries or a random sample of the entire history
        (double[][] trainingData, double[][] policyExpectedData, double[][] valueExpectedData) = telemetryHistory
            //.GetTrainingDataRandom(_telemetryHistory.NewEntries);
            .GetTrainingDataRandom(_telemetryHistory.Count);

        int i = -1;
        string vStop = string.Empty;
        string pStop = string.Empty;
        while (i < steps)
        {
            i++;

            // Get all new entries plus a little bit of the old entries or a random sample of the entire history
            //(double[][] trainingData, double[][] policyExpectedData, double[][] valueExpectedData) = Random.Shared.NextBoolean()
            //    ? telemetryHistory.GetTrainingDataRandom(_telemetryHistory.NewEntries)
            //    : telemetryHistory.GetTrainingDataNewFirst(_telemetryHistory.NewEntries);

            

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

                if (movingAverageValueError >= previousMovingAverageValueError)
                {
                    consecutiveIncreasesValueError++;
                }
                else if (!valueStopEarly)
                {
                    tempValueNetwork = mcts.ValueNetwork!.Clone();
                    clonedAtValueError = valueError;
                    consecutiveIncreasesValueError = 0;
                }

                previousMovingAverageValueError = movingAverageValueError;
            }

            if (i > movingAveragePolicy)
            {
                double movingAveragePolicyError = policyErrorHistory.Average();

                if (movingAveragePolicyError >= previousMovingAveragePolicyError)
                {
                    consecutiveIncreasesPolicyError++;
                }
                else if (!policyStopEarly)
                {
                    tempPolicyNetwork = mcts.PolicyNetwork!.Clone();
                    clonedAtPolicyError = policyError;
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

            if (!policyStopEarly && consecutiveIncreasesPolicyError >= ConsecutiveIncreaseLimit)
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

        string y = $"Current agent is {_teacherAgent?.Id ?? "None"} generation: {_teacherAgent?.Generation ?? 0}";
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
            TrainingProgress.IsBetterSignificantly => Color.FromArgb(164,83,255),
            TrainingProgress.IsBetter => Color.FromArgb(0,255,0),
            TrainingProgress.IsImproved => Color.FromArgb(200,200,200),
            TrainingProgress.IsFailed => Color.FromArgb(255,0,0),
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
        VsPlayParallel(Mcts mctsRed, Mcts mctsYellow, int mcstIterations, double explorationFactor, int? vsGames = null)
    {
        CancellationToken cancellationToken = _arenaCancelationSource.Token;

        int processorCount = Environment.ProcessorCount;
        int parallelGames = Math.Max(2, processorCount - 1);

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

                var redMcts = new Mcts(mcstIterations, mctsRed.ValueNetwork?.Clone(), mctsRed.PolicyNetwork?.Clone());
                var yellowMcts = new Mcts(mcstIterations, mctsYellow.ValueNetwork?.Clone(), mctsYellow.PolicyNetwork?.Clone());

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

                        int winner = game.PlacePieceColumn(move);
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
                }

                lock (sharedTelemetryHistory)
                {
                    _telemetryHistory.MergeFrom(redMcts.GetTelemetryHistory());
                    _telemetryHistory.MergeFrom(yellowMcts.GetTelemetryHistory());
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