using Connect4.Ais;
using Connect4.GameParts;
using DeepNetwork.NetworkIO;
using System.Collections.Concurrent;

namespace Connect4;

public partial class Form1 : Form
{
    private const int ColiseumGames = 300;

    public async Task BattleColiseum()
    {
        if (_agentCatalog.Entries.Count <= 0)
        {
            _ = MessageBox.Show("No agents in catalog. \nRun arena to generate some agents");
        }

        Agent[] agents = _agentCatalog.Entries.Values.ToArray();
        for (int i = agents.Length - 1; i >= 0; i--)
        {
            Agent agent1 = agents[i];
            agent1.ValueNetwork.ExplorationFactor = agent1.ExplorationFactor;
            agent1.PolicyNetwork.ExplorationFactor = agent1.ExplorationFactor;
            _ = BeginInvoke(() =>
            {
                _ = listBox1.Items.Add($"{agent1.Id} \tGen: {agent1.Generation} \tFactor: {agent1.ExplorationFactor:F2}");
                listBox1.SelectedIndex = listBox1.Items.Count - 1;
            });

            for (int j = agents.Length - 1; j >= 0; j--)
            {
                if (i == j)
                {
                    continue;
                }
                Agent agent2 = agents[j];
                agent2.ValueNetwork.ExplorationFactor = agent2.ExplorationFactor;
                agent2.PolicyNetwork.ExplorationFactor = agent2.ExplorationFactor;

                (int red, int yellow, int draw, int totals) = await PlayGames(agent1, agent2, ColiseumGames / 2);
                int redWins = red;
                int drawWins = draw;
                int totalWins = totals;

                (red, yellow, draw, totals)  = await PlayGames(agent2, agent1, ColiseumGames / 2);
                redWins += yellow;
                drawWins += draw;
                totalWins += totals;
                double redWithDrawPercent = (redWins + (drawWins * 0.5)) / totalWins * 100.0;

                _ = BeginInvoke(() =>
                {
                    string mark = redWithDrawPercent > 50 ? "✔" : "╳";
                    _ = listBox1.Items.Add($"{redWithDrawPercent:F2}% \t Gen: {agent2.Generation}  {mark}");
                    listBox1.SelectedIndex = listBox1.Items.Count - 1;
                });
            }
        }

        _ = BeginInvoke(() =>
        {
            toolStripStatusLabel1.Text = "Coliseum completed!";
            button4.Text = "Parallel Play";
            _isParallelSelfPlayRunning = false;
        });
    }

    private async Task<(int redWins, int yellowWins, int drawWins, int totalWins)> 
        PlayGames(Agent agent1, Agent agent2, int numberOfGames)
    {
        CancellationToken cancellationToken = _coliseimCancelationSource.Token;
        int processorCount = Environment.ProcessorCount;
        int parallelGames = Math.Max(2, processorCount - 1);

        int totalGames = numberOfGames > 0 ? numberOfGames : VsGames;

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

        ITrainingBuffer sharedTrainingBuffer = new TrainingBuffer();
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
                    var redMcts = new Mcts(McstIterations, agent1.ValueNetwork, agent1.PolicyNetwork);
                    var yellowMcts = new Mcts(McstIterations, agent2.ValueNetwork, agent2.PolicyNetwork);

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
                            Mcts mcts = game.CurrentPlayer == (int)Player.Red ? redMcts : yellowMcts;

                            double factor = mcts.ValueNetwork.ExplorationFactor;
                            int move = await mcts.GetBestMove(
                                game.GameBoard, 
                                (int)game.GameBoard.LastPlayed, 
                                factor,
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

        _ = BeginInvoke(() =>
        {
            UpdateGlobalStats(globalStats, totalGames);
        });

        // aggregate global stats to 1 final result
        int red = 0, yellow = 0, draw = 0, total = 0;
        foreach (var stat in globalStats.Values)
        {
            red += stat.Red;
            yellow += stat.Yellow;
            draw += stat.Draw;
            total += stat.Total;
        }

        return (red, yellow, draw, total);
    }
}