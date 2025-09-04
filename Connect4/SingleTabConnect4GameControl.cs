using Connect4.Ais;
using Connect4.GameParts;
using DeepNetwork.NetworkIO;

namespace Connect4
{
    public class SingleTabConnect4GameControl : UserControl
    {
        private readonly AgentCatalog _agentCatalog;
        private readonly Connect4Game _game = new();
        private readonly ComboBox _gameModeSelector;
        private readonly Mcts _mcts;
        private readonly Stack<string> _moveHistory = new();
        private readonly PictureBox _pictureBox;
        private readonly Button _resetButton;
        private readonly Button _undoButton;
        private Agent? _selectedAgent;
        private string _selectedGameMode = "Human vs Human";

        public SingleTabConnect4GameControl(Mcts mcts, AgentCatalog agentCatalog)
        {
            _mcts = mcts;
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
                Location = new Point(600, 311),
                Size = new Size(200, 23),
                DropDownStyle = ComboBoxStyle.DropDownList,
            };
            _gameModeSelector.Items.AddRange(["Human vs Human", "Human vs AI", "AI vs AI"]);
            _gameModeSelector.SelectedIndex = 0;
            _gameModeSelector.SelectedIndexChanged += GameModeSelector_SelectedIndexChanged;
            Controls.Add(_gameModeSelector);

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

            var agentGroupBox = new GroupBox
            {
                Text = "Agents",
                ForeColor = Color.White,
                Location = new Point(600, 11),
                Size = new Size(200, 300),
            };

            LoadAgentsIntoRadioButtons(agentGroupBox);
            Controls.Add(agentGroupBox);
        }

        private static void EndGame(
            Connect4Game connect4Game,
            Mcts mcts,
            PictureBox pictureBox,
            Stack<string> moveHistory)
        {
            mcts.SetWinnerTelemetryHistory(connect4Game.Winner);
            connect4Game.ResetGame();
            pictureBox.Invoke(pictureBox.Refresh);
            moveHistory.Clear();
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

        private void AgentRadioButton_CheckedChanged(object? sender, EventArgs e)
        {
            if (sender is RadioButton radioButton && radioButton.Checked)
            {
                _selectedAgent = radioButton.Tag as Agent;
                if (_selectedAgent != null)
                {
                    _mcts.PolicyNetwork = _selectedAgent.PolicyNetwork?.Clone();
                    _mcts.ValueNetwork = _selectedAgent.ValueNetwork?.Clone();
                }
            }
        }

        private void GameModeSelector_SelectedIndexChanged(object? sender, EventArgs e)
        {
            _selectedGameMode = _gameModeSelector.SelectedItem?.ToString() ?? "Human vs Human";
        }

        private void LoadAgentsIntoRadioButtons(GroupBox agentGroupBox)
        {
            _agentCatalog.LoadCatalog();
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
                radioButton.CheckedChanged += AgentRadioButton_CheckedChanged;
                agentGroupBox.Controls.Add(radioButton);
                y += 25;
            }

            if (agents.Count != 0)
            {
                if (agentGroupBox.Controls[0] is RadioButton firstRadioButton)
                {
                    firstRadioButton.Checked = true;
                }
            }
        }

        private void PerformAiMove()
        {
            int aiMove = _mcts.GetBestMove(_game.GameBoard, _game.CurrentPlayer, 1.0, _moveHistory.Count).Result;
            _game.PlacePieceColumn(aiMove);
            _pictureBox.Refresh();

            if (_game.GameBoard.HasWon(_game.CurrentPlayer))
            {
                string color = _game.CurrentPlayer == 1 ? "Red" : "Yellow";
                _ = MessageBox.Show($"{color} Player wins!");
                EndGame(_game, _mcts, _pictureBox, _moveHistory);
            }
            else if (_game.GameBoard.HasDraw())
            {
                _ = MessageBox.Show("It's a draw!");
                EndGame(_game, _mcts, _pictureBox, _moveHistory);
            }
        }

        private void PictureBox_Click(object? sender, EventArgs e)
        {
            if (_selectedGameMode == "AI vs AI")
            {
                return;
            }

            _moveHistory.Push(BitKey.ToKey(_game.GameBoard.StateToArray()));

            var clickEvent = e as MouseEventArgs;
            int winner = PlacePieceClick(_game, clickEvent, _pictureBox);

            if (winner != 0)
            {
                string color = winner == 1 ? "Red" : "Yellow";
                _ = MessageBox.Show($"{color} Player wins!");
                EndGame(_game, _mcts, _pictureBox, _moveHistory);
                return;
            }

            if (_game.GameBoard.HasDraw())
            {
                _ = MessageBox.Show("It's a draw!");
                EndGame(_game, _mcts, _pictureBox, _moveHistory);
                return;
            }

            if (_selectedGameMode == "Human vs AI" && _game.CurrentPlayer == 2)
            {
                PerformAiMove();
            }
        }

        private void PictureBox_Paint(object? sender, PaintEventArgs e)
        {
            _game.DrawBoard(e.Graphics);
        }

        private void ResetButton_Click(object? sender, EventArgs e)
        {
            ResetGameState(_game, _moveHistory, _pictureBox);

            if (_selectedGameMode == "AI vs AI")
            {
                StartAiVsAiGame();
            }
        }

        private async void StartAiVsAiGame()
        {
            while (_game.Winner == Winner.StillPlaying && !_game.GameBoard.HasDraw())
            {
                int aiMove = await _mcts.GetBestMove(_game.GameBoard, _game.CurrentPlayer, 1.0, _moveHistory.Count);
                _game.PlacePieceColumn(aiMove);
                _pictureBox.Refresh();

                if (_game.GameBoard.HasWon(_game.CurrentPlayer))
                {
                    string color = _game.CurrentPlayer == 1 ? "Red" : "Yellow";
                    _ = MessageBox.Show($"{color} Player wins!");
                    EndGame(_game, _mcts, _pictureBox, _moveHistory);
                    break;
                }

                if (_game.GameBoard.HasDraw())
                {
                    _ = MessageBox.Show("It's a draw!");
                    EndGame(_game, _mcts, _pictureBox, _moveHistory);
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
    }
}