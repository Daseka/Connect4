
using Connect4.Ais;
using Connect4.GameParts;
using DeepNetwork;

namespace Tests
{
    public class Tests
    {
        [Test]
        public void Test1()
        {
            Connect4Game connect4Game = new Connect4Game();
            Mcts mcts = new Mcts(10000);

            mcts.GetBestMove(connect4Game.GameBoard, 0, 1, 0);

            Assert.Pass();
        }

        [Test]
        public void Test2()
        {
            FlatDumbNetwork netowork = new FlatDumbNetwork([4, 2, 1]);
            var bla = netowork.Calculate([1, 0, 0, 1]);
        }
    }
}