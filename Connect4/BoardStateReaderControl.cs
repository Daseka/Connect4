using Connect4.Ais;
using Connect4.GameParts;
using DeepNetwork.NetworkIO;

namespace Connect4
{
    public class BoardStateReaderControl : UserControl
    {
        private readonly AgentCatalog _agentCatalog;
        private readonly Connect4Game _game = new();
        private readonly NumericUpDown _maxIterationsSelector;
        private readonly PictureBox _pictureBox;
        private readonly GroupBox _redAgentGroupBox;
        private readonly Mcts _redMcts;
        private readonly Button _resetButton;
        private Agent? _selectedAgent;

        public BoardStateReaderControl(AgentCatalog agentCatalog)
        {
            _redMcts = new Mcts(400);

            Dock = DockStyle.Fill;
            _agentCatalog = agentCatalog;

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

            _redAgentGroupBox = new GroupBox
            {
                Text = "Red Agent",
                ForeColor = Color.Red,
                Location = new Point(600, 11),
                Size = new Size(200, 300),
            };
            LoadAgentsIntoRadioButtons(_redAgentGroupBox);
            Controls.Add(_redAgentGroupBox);
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

        private async void ResetButton_Click(object? sender, EventArgs e)
        {
            _game.ResetGame();
            _pictureBox.Refresh();
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

        private void LoadAgentsIntoRadioButtons(GroupBox agentGroupBox)
        {
            Dictionary<string, Agent>.ValueCollection agents = _agentCatalog.Entries.Values;
            agentGroupBox.Controls.Clear();

            int y = 20;
            foreach (Agent agent in agents)
            {
                var radioButton = new RadioButton
                {
                    Text = $"{agent.Id} (Gen: {agent.Generation})",
                    Location = new Point(10, y),
                    AutoSize = true,
                    Tag = agent,
                };
                radioButton.CheckedChanged += RedAgentRadioButton_CheckedChanged;

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
            randomOption.CheckedChanged += RedAgentRadioButton_CheckedChanged;

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
            _redMcts.MaxIterations = (int)_maxIterationsSelector.Value;
        }

        private async void PictureBox_Click(object? sender, EventArgs e)
        {
            var clickEvent = e as MouseEventArgs;
            int winner = PlacePieceClick(_game, clickEvent, _pictureBox);
        }

        private void PictureBox_Paint(object? sender, PaintEventArgs e)
        {
            _game.DrawBoard(e.Graphics);
        }
    }
}