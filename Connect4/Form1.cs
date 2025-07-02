using Connect4.Ais;
using Connect4.GameParts;
using DeepNetwork;
using System.Diagnostics;

namespace Connect4;

public partial class Form1 : Form
{
    private const double MaximumError = 0.01;
    private const int MaxTrainingRuns = 5000;
    private const int McstIterations = 100;
    private const string OldPolicyNetwork = "old_policy_network.json";
    private const string OldValueNetwork = "old_value_network.json";
    private const int SelfPlayGames = 100;
    private readonly Connect4Game _connect4Game = new();
    private readonly SimpleDumbNetwork _newPolicyNetwork;
    private readonly SimpleDumbNetwork _newValueNetwork;
    private readonly System.Timers.Timer _timer = new() { Interval = 100 };
    private int _gamesPlayed = 0;
    private SimpleDumbNetwork _oldPolicyNetwork;
    private SimpleDumbNetwork _oldValueNetwork;
    private Mcts _redMcts;
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

    private static void TrainNetworks(Mcts mcts)
    {
        mcts.GetTelemetryHistory().LoadFromFile();

        //-----------------------
        //Train the value network
        (double[][] valueTrainingData, double[][] valueExpectedData) = mcts.GetTelemetryHistory().GetTrainingValueData();
        var valueTrainer = new NetworkTrainer(mcts.ValueNetwork);

        int run = 0;
        int sameCount = 0;
        double previousError = 0;
        double error;

        do
        {
            error = valueTrainer.Train(valueTrainingData, valueExpectedData);

            sameCount = previousError.Equals(error) ? sameCount + 1 : 0;
            previousError = error;
            run++;
        }
        while (run < MaxTrainingRuns && error > MaximumError && sameCount < 50);

        //-----------------------
        //Train the policy network
        (double[][] policyTrainingData, double[][] policyExpectedData) = mcts.GetTelemetryHistory().GetTrainingPolicyData();
        var policyTrainer = new NetworkTrainer(mcts.PolicyNetwork);

        run = 0;
        sameCount = 0;
        previousError = 0;

        do
        {
            error = policyTrainer.Train(policyTrainingData, policyExpectedData);

            sameCount = previousError.Equals(error) ? sameCount + 1 : 0;
            previousError = error;
            run++;
        }
        while (run < MaxTrainingRuns && error > MaximumError && sameCount < 50);
    }

    private void _timer_Elapsed(object? sender, System.Timers.ElapsedEventArgs e)
    {
        if (checkBox1.Checked)
        {
            int compMove = _redMcts.GetBestMove(_connect4Game.GameBoard, _connect4Game.CurrentPlayer == 1 ? 2 : 1);

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
        BattleArena();
    }

    private void BattleArena()
    {
        // Perform self-play games between the two MCTS instances
        SelfPlay(_redMcts, _yellowMcts, _connect4Game);

        // Load the old networks for the yellow MCTS instance
        _oldValueNetwork = SimpleDumbNetwork.CreateFromFile(OldValueNetwork) ?? _oldValueNetwork;
        _oldPolicyNetwork = SimpleDumbNetwork.CreateFromFile(OldPolicyNetwork) ?? _oldPolicyNetwork;
        _yellowMcts = new Mcts(McstIterations, _oldValueNetwork, _oldPolicyNetwork);

        // Save the telemetry history of the red MCTS instance
        _redMcts.GetTelemetryHistory().SaveToFile();

        Text += " - Training";

        // Train the red MCTS instance's networks
        TrainNetworks(_redMcts);

        // Save the trained network
        _redMcts.ValueNetwork?.SaveToFile(OldValueNetwork);
        _redMcts.PolicyNetwork?.SaveToFile(OldPolicyNetwork);

        //save and reset telemetry history
        TelemetryHistory telemetryHistory = _redMcts.GetTelemetryHistory();
        telemetryHistory.SaveToFile();
        telemetryHistory.ClearAll();
    }

    private void DisplayStats(int drawCount, int redWinCount, int yellowWinCount)
    {
        _ = Invoke(() => Text = $"Games played {++_gamesPlayed} " +
        $"Red: {Math.Round(redWinCount / (double)_gamesPlayed * 100, 2)}% " +
        $"Yellow: {Math.Round(yellowWinCount / (double)_gamesPlayed * 100, 2)}% " +
        $"Draw: {Math.Round(drawCount / (double)_gamesPlayed * 100, 2)}");
    }

    private void LoadButton_Click(object sender, EventArgs e)
    {
        _redMcts.GetTelemetryHistory().LoadFromFile();

        _oldValueNetwork = SimpleDumbNetwork.CreateFromFile(OldValueNetwork) ?? _oldPolicyNetwork;
        _oldPolicyNetwork = SimpleDumbNetwork.CreateFromFile(OldPolicyNetwork) ?? _oldPolicyNetwork;
        _redMcts = new Mcts(McstIterations, _oldValueNetwork, _oldPolicyNetwork);

        _ = MessageBox.Show("telemetry and network loaded successfully.");
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

        int compMove = _yellowMcts.GetBestMoveRandom(_connect4Game.GameBoard, _connect4Game.CurrentPlayer == 1 ? 2 : 1);
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

    private void SelfPlay(Mcts RedMcts, Mcts YellowMcts, Connect4Game game)
    {
        int count = 0;
        int drawCount = 0;
        int redWinCount = 0;
        int yellowWinCount = 0;

        while (count < SelfPlayGames)
        {
            Mcts mcts = game.CurrentPlayer == (int)Player.Red
                ? RedMcts
                : YellowMcts;

            int move = mcts.GetBestMove(game.GameBoard, (int)game.GameBoard.LastPlayed);
            if (move == -1)
            {
                EndGame(game, mcts, listBox1, pictureBox1);

                drawCount++;
                DisplayStats(drawCount, redWinCount, yellowWinCount);

                count++;
                continue;
            }

            int winner = PlacePiece(game, move, listBox1, pictureBox1);
            if (winner != 0)
            {
                EndGame(game, mcts, listBox1, pictureBox1);
                if (winner == 1)
                {
                    redWinCount++;
                }
                else if (winner == 2)
                {
                    yellowWinCount++;
                }

                DisplayStats(drawCount, redWinCount, yellowWinCount);

                count++;
                continue;
            }
        }

        DisplayStats(drawCount, redWinCount, yellowWinCount);
    }

    private void SelfPlayButton_Click(object sender, EventArgs e)
    {
        int count = 0;
        int drawCount = 0;
        int redWinCount = 0;
        int yellowWinCount = 0;
        while (count < SelfPlayGames)
        {
            int compMove = _redMcts.GetBestMoveRandom(_connect4Game.GameBoard, _connect4Game.CurrentPlayer == 1 ? 2 : 1);
            if (compMove == -1)
            {
                EndGame(_connect4Game, _redMcts, listBox1, pictureBox1);
                drawCount++;
                DisplayStats(drawCount, redWinCount, yellowWinCount);

                count++;
                continue;
            }

            int winner = PlacePiece(_connect4Game, compMove, listBox1, pictureBox1);
            if (winner != 0)
            {
                EndGame(_connect4Game, _redMcts, listBox1, pictureBox1);
                if (winner == 1)
                {
                    redWinCount++;
                }
                else if (winner == 2)
                {
                    yellowWinCount++;
                }

                DisplayStats(drawCount, redWinCount, yellowWinCount);

                count++;
                continue;
            }
        }

        DisplayStats(drawCount, redWinCount, yellowWinCount);
    }

    private Task TrainAsync()
    {
        _redMcts.GetTelemetryHistory().LoadFromFile();
        ////-----------------------
        ////Train the value network
        //(double[][] valueTrainingData, double[][] valueExpectedData) = _redMcts.GetTelemetryHistory().GetTrainingValueData();

        //int run = 0;
        //int sameCount = 0;
        //double previousError = 0;
        //double error;

        //var valueTrainer = new NetworkTrainer(_oldValueNetwork);

        //var stopwatch = Stopwatch.StartNew();
        //do
        //{
        //    error = valueTrainer.Train(valueTrainingData, valueExpectedData);

        //    sameCount = previousError.Equals(error) ? sameCount + 1 : 0;
        //    previousError = error;
        //    run++;
        //    Invoke(() =>
        //    {
        //        _ = listBox1.Items.Add($"Error {Math.Round(error, 3):F3}");
        //        listBox1.TopIndex = listBox1.Items.Count - 1;
        //    });
        //}
        //while (run < MaxTrainingRuns && error > MaximumError && sameCount < 50);
        //stopwatch.Stop();

        //string x = $"Training value completed in {stopwatch.ElapsedMilliseconds} ms after {run} runs with error {error}";
        //_ = Invoke(() => listBox1.Items.Add(x));
        ////Test the network 10 times
        //for (int i = 0; i < 10; i++)
        //{
        //    int index = i;
        //    double[] input = valueTrainingData[index];
        //    double[] output = _oldValueNetwork.Calculate(input);

        //    //calculate the error
        //    double expected = valueExpectedData[index][0];
        //    double diffrence = Math.Abs(expected - output[0]);
        //    Invoke(() =>
        //    {
        //        _ = listBox1.Items.Add($"Run {i:D2}: Difference {Math.Round(diffrence, 2):F2}");
        //        listBox1.TopIndex = listBox1.Items.Count - 1;
        //    });
        //}

        //-----------------------
        //Train the policy network
        (double[][] policyTrainingData, double[][] policyExpectedData) = _redMcts.GetTelemetryHistory().GetTrainingPolicyData();
        int run2 = 0;
        double sameCount2 = 0;
        double previousError2 = 0;
        double error2 = 0.0;

        var policyTrainer = new NetworkTrainer(_oldPolicyNetwork);

        var stopwatch2 = Stopwatch.StartNew();
        do
        {
            error2 = policyTrainer.Train(policyTrainingData, policyExpectedData);

            sameCount2 = previousError2.Equals(error2) ? sameCount2 + 1 : 0;
            previousError2 = error2;
            run2++;
            Invoke(() =>
            {
                listBox1.Items.Add($"Error {Math.Round(error2, 3):F3}");
                listBox1.TopIndex = listBox1.Items.Count - 1;
            });
        }
        while (run2 < MaxTrainingRuns && error2 > MaximumError && sameCount2 < 50);
        stopwatch2.Stop();

        var text = $"Training profile completed in {stopwatch2.ElapsedMilliseconds} ms after {run2} runs with error {error2}";
        listBox1.Items.Add(text);

        //Test the network 10 times
        for (int i = 0; i < 10; i++)
        {
            int index = i;
            double[] input = policyTrainingData[index];
            double[] output = _oldPolicyNetwork.Calculate(input);

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

    private async void TrainButton_Click(object sender, EventArgs e)
    {
        await Task.Run(TrainAsync);
    }
}