using Connect4.GameParts;
using DeepNetwork;

namespace Connect4.Ais;

public class Node
{
    private const int MovesThreshold = 10;
    private double[]? _cachedProbability = null;
    private double[]? _cachedValue = null;

    public List<Node> Children { get; }
    public GameBoard GameBoard { get; }
    public bool IsTerminal { get; set; }
    public int Move { get; set; }
    public Node? Parent { get; set; }
    public int PlayerToPlay { get; set; }
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

    public Node? GetBestChild(IStandardNetwork policyNetwork, double explorationFactor, Random random, bool isDeterministic)
    {
        double currentMaxUcb = double.MinValue;
        List<Node> bestChildren = [];
        Node? winningChild = null;

        double[] policyProbability = GetProbabilityCached(policyNetwork, GameBoard, ref _cachedProbability);

        if (!isDeterministic)
        {
            DirchletNoise.AddNoise(policyProbability, random);
        }

        foreach (Node node in Children)
        {
            double parentVisit = node.Parent?.Visits == 0 ? 1 : node.Parent?.Visits ?? 1;
            node.Ucb = node.Wins / (node.Visits == 0 ? 1 : node.Visits)
                + explorationFactor * policyProbability[node.Move] * Math.Sqrt(parentVisit / (1 + node.Visits));

            double roundedUcb = Math.Round(node.Ucb, 2);
            if (roundedUcb > currentMaxUcb)
            {
                currentMaxUcb = roundedUcb;
                bestChildren = [node];
            }
            else if (roundedUcb == currentMaxUcb)
            {
                bestChildren.Add(node);
            }

            if (node.GameBoard.HasWon((int)node.PLayerWhoMadeMove))
            {
                winningChild = node;
            }
        }

        //prioratize winning child over best child
        return winningChild ?? bestChildren[random.Next(bestChildren.Count)];
    }

    public Node? GetMostValuableChild(int movesPlayed, bool isDeterministic)
    {
        int index = SelectMoveFromVisits([..Children.Select(c => (int)c.Visits)], movesPlayed, isDeterministic);

        return index < 0 || index >= Children.Count 
            ? null 
            : Children[index];
    }

    /// <summary>
    /// If moves played higher than threshold or deterministic mode is on, then the most visited move is selected.
    /// </summary>
    private int SelectMoveFromVisits(int[] visitCounts, int movesPlayed, bool isDeterministic)
    {
        double temperature = movesPlayed < MovesThreshold ? 2 : 0;
        
        if (temperature == 0 || isDeterministic)
        {
            int mostVisited = 0;
            int maxVisits= 0;
            int? winningChild = null;
            for (int i = 0; i < visitCounts.Length; i++)
            {
                if (visitCounts[i] > maxVisits)
                {
                    maxVisits = visitCounts[i];
                    mostVisited = i;
                }

                if (Children[i].GameBoard.HasWon((int)Children[i].PLayerWhoMadeMove))
                {
                    winningChild = i;
                }
            }

            //prioratize winning child over best child
            return winningChild ?? mostVisited;
        }

        double[] weights = new double[visitCounts.Length];
        double sumWeights = 0;
        for (int i = 0; i < visitCounts.Length; i++)
        {
            if (visitCounts[i] > 0)
            {
                weights[i] = Math.Pow(visitCounts[i], 1/ temperature);
                sumWeights += weights[i];
            }
        }

        double selectedValue = Random.Shared.NextDouble() * sumWeights;
        for (int i = 0; i < weights.Length; i++)
        {
            if (weights[i] <= 0)
            {
                continue;
            }

            selectedValue -= weights[i];
            if (selectedValue <= 0)
            {
                return i;
            }
        }

        return Array.IndexOf(visitCounts, visitCounts.Max());
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

        Wins += result;
    }

    public double[] GetValueCached(IStandardNetwork valueNetwork)
    {
        if (_cachedValue is not null)
        {
            return _cachedValue;
        }

        _cachedValue = valueNetwork.Calculate([.. GameBoard.StateToArray().Select(x => (double)x)]);

        return _cachedValue;
    }

    private static double[] GetProbabilityCached(
        IStandardNetwork policyNetwork, 
        GameBoard gameBoard, 
        ref double[]? cachedProbability)
    {
        if (cachedProbability is not null)
        {
            return cachedProbability;
        }

        cachedProbability = policyNetwork.Calculate([.. gameBoard.StateToArray().Select(x => (double)x)]);

        return cachedProbability;
    }
}