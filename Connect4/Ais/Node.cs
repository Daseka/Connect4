using Connect4.GameParts;
using DeepNetwork;

namespace Connect4.Ais;
public class Node
{
    //private const double ExplorationConstant =0.8;
    public List<Node> Children { get; }
    public GameBoard GameBoard { get; }
    public bool IsTerminal { get; set; }
    public int Move { get; set; }
    public Node? Parent { get; }
    public int PlayerToPlay { get; set; }

    // This is the player who made the move that led to this node
    public int PLayerWhoMadeMove { get; set; } = 0;

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

    public Node? GetBestChild(double explorationFactor)
    {
        double currentMaxUcb = double.MinValue;
        Node? bestChild = null;

        foreach (Node node in Children)
        {
            node.Ucb = node.Visits == 0
                ? double.MaxValue
                : node.Wins / node.Visits + explorationFactor * Math.Sqrt(Math.Log(node.Parent?.Visits ?? 1) / node.Visits);

            if (node.Ucb > currentMaxUcb)
            {
                currentMaxUcb = node.Ucb;
                bestChild = node;
            }
        }

        return bestChild;
    }

    public Node? GetBestChild(IStandardNetwork policyNetwork, double explorationFactor)
    {
        double currentMaxUcb = double.MinValue;
        Node? bestChild = null;
        Node? winningChild = null;

        double[] boardStateArray = [.. GameBoard.StateToArray().Select(x => (double)x)];
        string id = GameBoard.StateToString();
        double[] policyProbability = policyNetwork.CalculateCached(id, boardStateArray);

        
        //DirchletNoise.AddNoise(policyProbability, random);
        

        foreach (Node node in Children)
        {
            double parentVisit = node.Parent?.Visits == 0 ? 1 : node.Parent?.Visits ?? 1;
            node.Ucb = node.Wins / (node.Visits == 0 ? 1 : node.Visits)
                + explorationFactor * policyProbability[node.Move] * Math.Sqrt(parentVisit / (1 + node.Visits));

            if (node.Ucb > currentMaxUcb)
            {
                currentMaxUcb = node.Ucb;
                bestChild = node;
            }

            if (node.GameBoard.HasWon((int)node.PLayerWhoMadeMove))
            {
                winningChild = node;
            }
        }

        //prioratize winning child over best child
        return winningChild ?? bestChild;
    }

    public Node? GetMostValuableChild()
    {
        double currentMaxValue = double.MinValue;
        double currentMinVisits = double.MinValue;
        Node? bestChild = null;

        foreach (Node node in Children)
        {
            double value = node.Visits == 0 ? 0 : node.Wins / node.Visits;
            if (value > currentMaxValue || (value == currentMaxValue && node.Visits < currentMinVisits))
            {
                currentMinVisits = node.Visits;
                currentMaxValue = value;
                bestChild = node;
            }
        }

        return bestChild;
    }

    public bool IsLeaf()
    {
        return Children.Count == 0;
    }

    public override string ToString()
    {
        return $"Move: {Move} Wins: {Math.Round(Wins, 2)} V:{Visits} Ucb: {Math.Round(Ucb, 2)} Val: {Math.Round(Wins / Visits, 2)}";
    }

    public void Update(double result)
    {
        ++Visits;

        Wins += result > 0
            ? Math.Round(result, 4)
            : Math.Round(1 + result, 4);
    }
}