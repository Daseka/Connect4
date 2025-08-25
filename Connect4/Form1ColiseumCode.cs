using Connect4.Ais;
using Connect4.GameParts;
using DeepNetwork.NetworkIO;
using System.Collections.Concurrent;

namespace Connect4;

public partial class Form1 : Form
{
    private const int ColiseumGames = 100;

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

            for (int j = i - 1; j >= 0; j--)
            {
                Agent agent2 = agents[j];
                agent2.ValueNetwork.ExplorationFactor = agent2.ExplorationFactor;
                agent2.PolicyNetwork.ExplorationFactor = agent2.ExplorationFactor;

                await PlayGames(agent1, agent2, ColiseumGames);
            }
        }
    }

    private async Task PlayGames(Agent agent1, Agent agent2, int numberOfGames)
    {
        CancellationToken cancellationToken = _coliseimCancelationSource.Token;
        int processorCount = Environment.ProcessorCount;
        int parallelGames = Math.Max(2, processorCount - 1);

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
            string mark = _redWithDrawPercent > 50 ? "✔" : "╳";
            _ = listBox1.Items.Add($"Gen: {agent2.Generation} \t{_redWithDrawPercent:F2}%   {mark}");
            listBox1.SelectedIndex = listBox1.Items.Count - 1;

            if (!cancellationToken.IsCancellationRequested)
            {
                toolStripStatusLabel1.Text = "All parallel games completed!";
                button4.Text = "Parallel Play";
                _isParallelSelfPlayRunning = false;
            }
        });
    }
}