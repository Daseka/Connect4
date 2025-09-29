using Connect4.Ais;
using Connect4.GameParts;
using DeepNetwork.NetworkIO;
using System.Net;

namespace Connect4
{
    public class SingleTabConnect4GameControl : UserControl
    {
        private readonly AgentCatalog _agentCatalog;
        private readonly List<Bitmap> _boardStateHistory = [];
        private readonly Connect4Game _game = new();

        private readonly string[] _gameModes =
        [
            "Human vs Human",
            "Human vs AI",
            "AI vs Human",
            "AI vs AI"
        ];

        private readonly ComboBox _gameModeSelector;
        private readonly Panel _historyPanel;
        private readonly NumericUpDown _maxIterationsSelector;
        private readonly Stack<string> _moveHistory = new();
        private readonly PictureBox _pictureBox;
        private readonly GroupBox _redAgentGroupBox;
        private readonly Mcts _redMcts;
        private readonly Button _resetButton;
        private readonly Button _undoButton;
        private readonly GroupBox _yellowAgentGroupBox;
        private readonly Mcts _yellowMcts;
        private Agent? _selectedAgent;
        private string _selectedGameMode = "Human vs Human";
        private GroupBox _remotConnectionGroupbox;

        public SingleTabConnect4GameControl(AgentCatalog agentCatalog)
        {
            _yellowMcts = new Mcts(400);
            _redMcts = new Mcts(400);

            _agentCatalog = agentCatalog;
            Dock = DockStyle.Fill;

            _pictureBox = new PictureBox
            {
                BackColor = Color.Black,
                Location = new Point(6, 36),
                Size = new Size(450, 320),
                TabStop = false,
            };
            _pictureBox.Paint += PictureBox_Paint;
            _pictureBox.Click += PictureBox_Click;
            Controls.Add(_pictureBox);

            _gameModeSelector = new ComboBox
            {
                Location = new Point(900, 55),
                Size = new Size(160, 23),
                DropDownStyle = ComboBoxStyle.DropDownList,
            };
            _gameModeSelector.Items.AddRange(_gameModes);
            _gameModeSelector.SelectedIndex = 0;
            _gameModeSelector.SelectedIndexChanged += GameModeSelector_SelectedIndexChanged;
            Controls.Add(_gameModeSelector);

            _maxIterationsSelector = new NumericUpDown
            {
                Location = new Point(900, 80),
                Size = new Size(160, 23),
                Minimum = 400,
                Maximum = 100000,
                Value = 400,
                Increment = 100,
            };
            _maxIterationsSelector.ValueChanged += MaxIterationsSelector_ValueChanged;
            Controls.Add(_maxIterationsSelector);

            _resetButton = new Button
            {
                Location = new Point(900, 11),
                Size = new Size(75, 23),
                Text = "Restart",
            };
            _resetButton.Click += ResetButton_Click;
            Controls.Add(_resetButton);

            _undoButton = new Button
            {
                Location = new Point(1000, 11),
                Size = new Size(75, 23),
                Text = "Undo",
            };
            _undoButton.Click += UndoButton_Click;
            Controls.Add(_undoButton);

            _redAgentGroupBox = new GroupBox
            {
                Text = "Red Agent",
                ForeColor = Color.Red,
                Location = new Point(600, 11),
                Size = new Size(200, 300),
            };
            LoadAgentsIntoRadioButtons(_redAgentGroupBox, isRed: true);
            Controls.Add(_redAgentGroupBox);

            _yellowAgentGroupBox = new GroupBox
            {
                Text = "Yellow Agent",
                ForeColor = Color.Yellow,
                Location = new Point(600, 310),
                Size = new Size(200, 300),
            };
            LoadAgentsIntoRadioButtons(_yellowAgentGroupBox, isRed: false);
            Controls.Add(_yellowAgentGroupBox);

            _historyPanel = new Panel
            {
                Location = new Point(1100, 11),
                Size = new Size(250, 800),
                BorderStyle = BorderStyle.FixedSingle,
                AutoScroll = true,
            };
            Controls.Add(_historyPanel);

            _remotConnectionGroupbox = new GroupBox
            {
                Text = "Remote Connection",
                ForeColor = Color.White,
                Location = new Point(100, 480),
                Size = new Size(400, 100),
            };
            LoadRemoteComponents(_remotConnectionGroupbox);
            Controls.Add(_remotConnectionGroupbox);
        }

        private void LoadRemoteComponents(GroupBox remotConnectionGroupbox)
        {
            IPAddress? v = Dns.GetHostAddresses(Dns.GetHostName())
                .FirstOrDefault(ip => ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork);
            string ipAddres = v?.ToString() ?? string.Empty;


            var ipLabel = new Label
            {
                Text = $"My IP: {ipAddres}",
                Location = new Point(10, 20),
                AutoSize = true,
                Font = new Font(FontFamily.GenericMonospace, 15, FontStyle.Bold),
            };
            remotConnectionGroupbox.Controls.Add(ipLabel);
        }

        private static int PlacePieceClick(
            Connect4Game connect4Game,
            MouseEventArgs? clickEvent,
            PictureBox pictureBox)
        {
            int winner = connect4Game.PlacePieceClick(clickEvent, pictureBox);
            pictureBox.Invoke(pictureBox.Refresh);
            return winner;
        }

        private static void ResetGameState(Connect4Game game, Stack<string> moveHistory, PictureBox pictureBox)
        {
            game.ResetGame();
            moveHistory.Clear();
            pictureBox.Refresh();
        }

        private void DisplayBoardStateHistory()
        {
            _historyPanel.Controls.Clear();

            int yOffset = 10;
            foreach (var boardState in _boardStateHistory)
            {
                var pictureBox = new PictureBox
                {
                    Image = boardState,
                    Size = new Size(180, 128),
                    Location = new Point(10, yOffset),
                    SizeMode = PictureBoxSizeMode.StretchImage,
                };
                _historyPanel.Controls.Add(pictureBox);
                yOffset += 138;
            }
        }

        private void EndGame(
            Connect4Game connect4Game,
            Mcts mcts,
            PictureBox pictureBox,
            Stack<string> moveHistory)
        {
            mcts.SetWinnerTelemetryHistory(connect4Game.Winner);
            connect4Game.ResetGame();
            pictureBox.Invoke(pictureBox.Refresh);
            moveHistory.Clear();

            this.DisplayBoardStateHistory();
        }

        private void GameModeSelector_SelectedIndexChanged(object? sender, EventArgs e)
        {
            _selectedGameMode = _gameModeSelector.SelectedItem?.ToString() ?? "Human vs Human";
            // If AI vs Human is selected and game is fresh, let AI start
            if (_selectedGameMode == "AI vs Human")
            {
                if (_moveHistory.Count == 0 && _game.CurrentPlayer == 1)
                {
                    PerformAiMove(_redMcts); // AI (Red) starts
                }
            }
        }

        private void LoadAgentsIntoRadioButtons(GroupBox agentGroupBox, bool isRed)
        {
            var agents = _agentCatalog.Entries.Values;
            agentGroupBox.Controls.Clear();

            int y = 20;
            foreach (var agent in agents)
            {
                var radioButton = new RadioButton
                {
                    Text = $"{agent.Id} (Gen: {agent.Generation})",
                    Location = new Point(10, y),
                    AutoSize = true,
                    Tag = agent,
                };
                radioButton.CheckedChanged += isRed
                    ? RedAgentRadioButton_CheckedChanged
                    : YellowAgentRadioButton_CheckedChanged;

                agentGroupBox.Controls.Add(radioButton);
                y += 25;
            }

            var randomOption = new RadioButton
            {
                Text = "Random",
                Location = new Point(10, y),
                AutoSize = true,
                Tag = new Agent
                {
                    Id = "Random",
                    PolicyNetwork = null,
                    ValueNetwork = null
                }
            };
            randomOption.CheckedChanged += isRed
                    ? RedAgentRadioButton_CheckedChanged
                    : YellowAgentRadioButton_CheckedChanged;

            agentGroupBox.Controls.Add(randomOption);

            if (agents.Count != 0)
            {
                if (agentGroupBox.Controls[0] is RadioButton firstRadioButton)
                {
                    firstRadioButton.Checked = true;
                }
            }
        }

        private void MaxIterationsSelector_ValueChanged(object? sender, EventArgs e)
        {
            _yellowMcts.MaxIterations = (int)_maxIterationsSelector.Value;
            _redMcts.MaxIterations = (int)_maxIterationsSelector.Value;
        }

        private void PerformAiMove(Mcts mcts)
        {
            int aiMove = mcts.GetBestMove(_game.GameBoard, (int)_game.GameBoard.LastPlayed, 1.0, _moveHistory.Count, true)
                .GetAwaiter()
                .GetResult();
            _game.PlacePieceColumn(aiMove);
            _pictureBox.Refresh();

            UpdateBoardStateHistory();

            if (_game.GameBoard.HasWon(_game.CurrentPlayer))
            {
                string color = _game.CurrentPlayer == 1 ? "Red" : "Yellow";
                _ = MessageBox.Show($"{color} Player wins!");
                EndGame(_game, mcts, _pictureBox, _moveHistory);
            }
            else if (_game.GameBoard.HasDraw())
            {
                _ = MessageBox.Show("It's a draw!");
                EndGame(_game, mcts, _pictureBox, _moveHistory);
            }
        }

        private void PictureBox_Click(object? sender, EventArgs e)
        {
            if (_selectedGameMode == "AI vs AI" || (_selectedGameMode == "AI vs Human" && _game.CurrentPlayer == 1))
            {
                // Ignore clicks when it's AI's turn in AI vs Human mode
                return;
            }

            _moveHistory.Push(BitKey.ToKey(_game.GameBoard.StateToArray()));

            var clickEvent = e as MouseEventArgs;
            int winner = PlacePieceClick(_game, clickEvent, _pictureBox);

            UpdateBoardStateHistory();

            var mcts = _game.CurrentPlayer == 1 ? _redMcts : _yellowMcts;
            if (winner != 0)
            {
                string color = winner == 1 ? "Red" : "Yellow";
                _ = MessageBox.Show($"{color} Player wins!");
                EndGame(_game, mcts, _pictureBox, _moveHistory);
            }
            else if (_game.GameBoard.HasDraw())
            {
                _ = MessageBox.Show("It's a draw!");
                EndGame(_game, mcts, _pictureBox, _moveHistory);
            }
            else if (_selectedGameMode == "Human vs AI" && _game.CurrentPlayer == 2)
            {
                PerformAiMove(mcts);
            }
            else if (_selectedGameMode == "AI vs Human")
            {
                // Let AI keep playing until it's the human's turn or the game ends
                while (_game.CurrentPlayer == 1 && _game.Winner == Winner.StillPlaying && !_game.GameBoard.HasDraw())
                {
                    PerformAiMove(_redMcts);
                }
            }
        }

        private void PictureBox_Paint(object? sender, PaintEventArgs e)
        {
            _game.DrawBoard(e.Graphics);
        }

        private void RedAgentRadioButton_CheckedChanged(object? sender, EventArgs e)
        {
            if (sender is RadioButton radioButton && radioButton.Checked)
            {
                _selectedAgent = radioButton.Tag as Agent;
                if (_selectedAgent != null)
                {
                    _redMcts.PolicyNetwork = _selectedAgent.PolicyNetwork?.Clone();
                    _redMcts.ValueNetwork = _selectedAgent.ValueNetwork?.Clone();
                }
            }
        }

        private void ResetButton_Click(object? sender, EventArgs e)
        {
            ResetGameState(_game, _moveHistory, _pictureBox);

            if (_selectedGameMode == "AI vs AI")
            {
                StartAiVsAiGame();
            }
            else if (_selectedGameMode == "AI vs Human")
            {
                // AI (Red) starts
                PerformAiMove(_redMcts);
            }
        }

        private async void StartAiVsAiGame()
        {
            Mcts mcts;
            while (_game.Winner == Winner.StillPlaying && !_game.GameBoard.HasDraw())
            {
                mcts = _game.CurrentPlayer == 1 ? _redMcts : _yellowMcts;
                int aiMove = await mcts.GetBestMove(_game.GameBoard, (int)_game.GameBoard.LastPlayed, 1.0, _moveHistory.Count, true);
                _game.PlacePieceColumn(aiMove);
                _pictureBox.Refresh();

                UpdateBoardStateHistory();

                if (_game.GameBoard.HasWon(_game.CurrentPlayer))
                {
                    string color = _game.CurrentPlayer == 1 ? "Red" : "Yellow";
                    _ = MessageBox.Show($"{color} Player wins!");
                    EndGame(_game, mcts, _pictureBox, _moveHistory);
                    break;
                }

                if (_game.GameBoard.HasDraw())
                {
                    _ = MessageBox.Show("It's a draw!");
                    EndGame(_game, mcts, _pictureBox, _moveHistory);
                    break;
                }
            }
        }

        private void UndoButton_Click(object? sender, EventArgs e)
        {
            if (_moveHistory.Count > 0)
            {
                string prevState = _moveHistory.Pop();
                _game.SetState(prevState);
                _game.CurrentPlayer = _game.CurrentPlayer == 1 ? 2 : 1;
                _pictureBox.Refresh();
            }
        }

        private void UpdateBoardStateHistory()
        {
            if (_boardStateHistory.Count == 5)
            {
                _boardStateHistory.RemoveAt(0);
            }

            var bitmap = new Bitmap(_pictureBox.Width, _pictureBox.Height);
            _pictureBox.DrawToBitmap(bitmap, new Rectangle(0, 0, _pictureBox.Width, _pictureBox.Height));
            _boardStateHistory.Add(bitmap);
        }

        private void YellowAgentRadioButton_CheckedChanged(object? sender, EventArgs e)
        {
            if (sender is RadioButton radioButton && radioButton.Checked)
            {
                _selectedAgent = radioButton.Tag as Agent;
                if (_selectedAgent != null)
                {
                    _yellowMcts.PolicyNetwork = _selectedAgent.PolicyNetwork?.Clone();
                    _yellowMcts.ValueNetwork = _selectedAgent.ValueNetwork?.Clone();
                }
            }
        }
    }
}