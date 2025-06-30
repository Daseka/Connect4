
using Connect4.Ais;
using Connect4.GameParts;

namespace Tests
{
    public class Tests
    {
        [Test]
        public void Test1()
        {
            Connect4Game connect4Game = new Connect4Game();
            Mcts mcts = new Mcts(10000);

            mcts.GetBestMove(connect4Game.GameBoard, 0);

            Assert.Pass();
        }
    }   
}