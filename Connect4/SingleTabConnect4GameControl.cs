using Connect4.Ais;
using Connect4.GameParts;
using System.Collections.Generic;

namespace Connect4
{
    public class SingleTabConnect4GameControl : UserControl
    {
        private readonly Connect4Game _game = new();
        private readonly Mcts _mcts;
        private readonly PictureBox _pictureBox;
        private readonly Button _resetButton;
        private readonly Button _undoButton;
        private readonly Stack<string> _moveHistory = new(); // Store board states for undo

        public SingleTabConnect4GameControl(Mcts mcts)
        {
            _mcts = mcts;
            Dock = DockStyle.Fill;

            _pictureBox = new PictureBox
            {
                BackColor = Color.Black,
                Location = new Point(6, 36),
                Size = new Size(650, 320),
                TabStop = false,
            };
            _pictureBox.Paint += PictureBox_Paint;
            _pictureBox.Click += PictureBox_Click;
            Controls.Add(_pictureBox);

            _resetButton = new Button
            {
                Location = new Point(700, 11),
                Size = new Size(75, 23),
                Text = "Reset game",
            };
            _resetButton.Click += ResetButton_Click;
            Controls.Add(_resetButton);

            _undoButton = new Button
            {
                Location = new Point(800, 11),
                Size = new Size(75, 23),
                Text = "Undo",
            };
            _undoButton.Click += UndoButton_Click;
            Controls.Add(_undoButton);
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

        private void PictureBox_Click(object? sender, EventArgs e)
        {
            // Save current state before move for undo
            _moveHistory.Push(BitKey.ToKey( _game.GameBoard.StateToArray()));
            var clickEvent = e as MouseEventArgs;
            int winner = PlacePieceClick(_game, clickEvent, _pictureBox);

            if (winner != 0)
            {
                string color = winner == 1 ? "Red" : "Yellow";
                _ = MessageBox.Show($"{color} Player wins!");
                EndGame(_game, _mcts, _pictureBox, _moveHistory);
            }

            if (_game.GameBoard.HasDraw())
            {
                _ = MessageBox.Show("It's a draw!");
                EndGame(_game, _mcts, _pictureBox, _moveHistory);
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

        private void PictureBox_Paint(object? sender, PaintEventArgs e)
        {
            _game.DrawBoard(e.Graphics);
        }

        private void ResetButton_Click(object? sender, EventArgs e)
        {
            _game.ResetGame();
            _pictureBox.Refresh();
            _moveHistory.Clear();
        }
    }
}