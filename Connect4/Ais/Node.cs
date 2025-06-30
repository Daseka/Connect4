using Connect4.GameParts;
using DeepNetwork;

namespace Connect4.Ais;

public class Node
{
    private const double ExplorationConstant = 1.41;
    public List<Node> Children { get; }
    public GameBoard GameBoard { get; }
    public bool IsTerminal { get; set; }
    public int Move { get; set; }
    public Node? Parent { get; }
    // This is the player who made the move that led to this node
    public int PLayerWhoMadeMove { get; set; } = 0; 
    public int PlayerToPlay { get; set; }
    public double Ucb { get; set; }
    public double Visits { get; private set; }
    public double Wins { get; private set; }

    public Node(GameBoard gameBoard, int previousPlayer)
    {
        Children = [];
        GameBoard = gameBoard;
        Parent = null;
        Move = -1;
        PLayerWhoMadeMove = previousPlayer;
        PlayerToPlay = previousPlayer == 1 ? 2 : 1;
    }

    public Node(GameBoard gameBoard, Node parent, int move = -1)
    {
        Children = [];
        GameBoard = gameBoard;
        Parent = parent;
        Move = move;
        PLayerWhoMadeMove = parent?.PlayerToPlay ?? 0;
        PlayerToPlay = parent?.PlayerToPlay == 1 ? 2 : 1;
    }

    public Node? GetBestChild()
    {
        double currentMaxUcb = double.MinValue;
        Node? bestChild = null;

        foreach (Node node in Children)
        {
            node.Ucb = node.Visits == 0
                ? double.MaxValue
                : node.Wins / node.Visits + ExplorationConstant * Math.Sqrt(Math.Log(node.Parent?.Visits ?? 1) / node.Visits);

            if (node.Ucb > currentMaxUcb)
            {
                currentMaxUcb = node.Ucb;
                bestChild = node;
            }
        }

        return bestChild;
    }

    public Node? GetBestChild(SimpleDumbNetwork policyNetwork)
    {
        double currentMaxUcb = double.MinValue;
        Node? bestChild = null;

        double[] boardStateArray = [.. GameBoard.StateToArray().Select(x => (double)x)];
        double[] policyProbability = policyNetwork.Calculate(boardStateArray);

        foreach (Node node in Children)
        {
            node.Ucb = node.Wins / node.Visits 
                + ExplorationConstant * policyProbability[node.Move] * Math.Sqrt((node.Parent?.Visits ?? 1) / (1 + node.Visits));

            //node.Ucb = node.Visits == 0
            //    ? double.MaxValue
            //    : node.Wins / node.Visits + ExplorationConstant * Math.Sqrt(Math.Log(node.Parent?.Visits ?? 1) / node.Visits);

            if (node.Ucb > currentMaxUcb)
            {
                currentMaxUcb = node.Ucb;
                bestChild = node;
            }
        }

        return bestChild;
    }

    public bool IsLeaf()
    {
        return Children.Count == 0;
    }

    public void Update(int result)
    {
        ++Visits;
        if (result == PLayerWhoMadeMove)
        {
            ++Wins;
        }
    }
}