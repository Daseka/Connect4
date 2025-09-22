using Connect4.GameParts;
using DeepNetwork;
using System.Diagnostics;

namespace Connect4.Ais;

public class Mcts(
    int maxIterations,
    IStandardNetwork? valueNetwork = null,
    IStandardNetwork? policyNetwork = null,
    Random? random = null)
{
    private const int MaxColumnCount = 7;
    private const double MinimumPolicyValue = 0.001;
    private readonly Random _random = random ?? new();
    private readonly TelemetryHistory _telemetryHistory = new();
    private Node? _rootNode;

    public int MaxIterations { get; set; } = maxIterations;
    public IStandardNetwork? PolicyNetwork { get; set; } = policyNetwork;
    public IStandardNetwork? ValueNetwork { get; set; } = valueNetwork;

    public Task<int> GetBestMove(
        GameBoard gameBoard,
        int previousPlayer,
        double explorationFactor,
        int movesPlayed,
        bool isDeterministic = false)
    {
        var rootNode = FindRootNode(gameBoard, previousPlayer);
        bool useNetworks = PolicyNetwork?.Trained == true && ValueNetwork?.Trained == true;

        var stopwatch = Stopwatch.StartNew();
        for (int i = 0; i < MaxIterations; i++)
        //while (stopwatch.ElapsedMilliseconds < _maxMiliseconds)
        {
            Node? childNode = useNetworks
                ? Select(rootNode, _random, PolicyNetwork!, explorationFactor, isDeterministic)
                : Select(rootNode, _random, explorationFactor);

            double result = useNetworks
                ? Simulate(childNode, ValueNetwork!)
                : Simulate(childNode, _random);

            Backpropagate(childNode, result);
        }
        stopwatch.Stop();
        UpdateTelemetryHistory(rootNode, _telemetryHistory);

        var bestChild = rootNode.GetMostValuableChild(movesPlayed, isDeterministic);
        if (bestChild != null)
        {
            bestChild.Parent = null;
        }

        _rootNode = bestChild;

        return Task.FromResult(bestChild?.Move ?? -1);
    }

    public TelemetryHistory GetTelemetryHistory()
    {
        return _telemetryHistory;
    }

    public void SetWinnerTelemetryHistory(Winner winner)
    {
        _telemetryHistory.StoreWinnerData(winner);
    }

    private static void Backpropagate(Node node, double result)
    {
        // If the result is 0, we set it to a small value to enable switching between players
        result = result == 0 ? 1e-9 : result;

        Node? currentNode = node;
        while (currentNode != null)
        {
            currentNode.Update(result);
            //flip the result for the next parent node to indicate the other player's perspective
            result *= -1;

            currentNode = currentNode.Parent;
        }
    }

    private static double DeepSimulation(Node node, IStandardNetwork valueNetwork)
    {
        //if last player won no need to calculate value of network
        if (node.GameBoard.HasWon((int)node.GameBoard.LastPlayed))
        {
            node.IsTerminal = true;
            return 1;
        }

        // if node is terminal and previous player didnt win then it's a draw
        if (node.GameBoard.HasDraw())
        {
            node.IsTerminal = true;
            return 0;
        }

        double[] winProbability = node.GetValueCached(valueNetwork);

        return winProbability[0];
    }

    private static Node Expand(Node node, Random random)
    {
        int[] availableMoves = node.GameBoard.GetAvailableColumns();
        if (availableMoves.Length == 0)
        {
            // No available moves, return the node as terminal
            node.IsTerminal = true;

            return node;
        }

        foreach (int move in availableMoves)
        {
            GameBoard newGameBoard = node.GameBoard.Copy();
            _ = newGameBoard.PlacePiece(move, node.PlayerToPlay);
            var childNode = new Node(newGameBoard, node, move)
            {
                PlayerToPlay = node.PlayerToPlay == 1 ? 2 : 1
            };

            node.Children.Add(childNode);
        }

        return node.Children.Count > 0
            ? node.Children[random.Next(node.Children.Count)]
            : node;
    }

    private static double RandomSimulation(Node node, Random random)
    {
        // Check if the game was won by the selected moved of the node
        int previousPlayer = node.PLayerWhoMadeMove;
        if (node.GameBoard.HasWon(previousPlayer))
        {
            node.IsTerminal = true;
            return 1;
        }

        if (node.IsTerminal)
        {
            // if node is terminal and previous player didnt win then it's a draw
            return 0;
        }

        Node currentNode = node;
        GameBoard gameBoardCopy = currentNode.GameBoard.Copy();
        int currentPlayer = currentNode.PlayerToPlay;

        int[] availableMoves = gameBoardCopy.GetAvailableColumns();

        while (!gameBoardCopy.HasWon(previousPlayer) && availableMoves.Length > 0)
        {
            int move = availableMoves[random.Next(availableMoves.Length)];
            _ = gameBoardCopy.PlacePiece(move, currentPlayer);

            //switch players
            previousPlayer = currentPlayer;
            currentPlayer = currentPlayer == 1 ? 2 : 1;

            //get new available moves
            availableMoves = gameBoardCopy.GetAvailableColumns();
        }

        if (availableMoves.Length == 0)
        {
            // No available moves, it's a draw
            return 0;
        }

        // Return win value
        return previousPlayer == 1 ? 1 : 0;
    }

    private static Node Select(Node node, Random random, double explorationFactor)
    {
        if (node.IsTerminal)
        {
            return node;
        }

        if (node.IsLeaf())
        {
            return Expand(node, random);
        }

        Node? bestChild = node.GetBestChild(explorationFactor);
        while (bestChild is not null && bestChild.Visits > 0 && !bestChild.IsTerminal)
        {
            bestChild = Select(bestChild, random, explorationFactor);
        }

        return bestChild ?? node;
    }

    private static Node Select(
        Node node,
        Random random,
        IStandardNetwork policyNetwork,
        double explorationFactor,
        bool isDeterministic)
    {
        if (node.IsTerminal)
        {
            return node;
        }

        if (node.IsLeaf())
        {
            return Expand(node, random);
        }

        Node? bestChild = node.GetBestChild(policyNetwork, explorationFactor, random, isDeterministic);
        while (bestChild is not null && bestChild.Visits > 0 && !bestChild.IsTerminal)
        {
            bestChild = Select(bestChild, random, policyNetwork, explorationFactor, isDeterministic);
        }

        return bestChild ?? node;
    }

    private static double Simulate(Node node, Random random)
    {
        return RandomSimulation(node, random);
    }

    private static double Simulate(Node node, IStandardNetwork valueNetwork)
    {
        return DeepSimulation(node, valueNetwork);
    }

    private static void UpdateTelemetryHistory(Node root, TelemetryHistory telemetryHistory)
    {
        double[] policy = new double[MaxColumnCount];
        foreach (Node child in root.Children)
        {
            policy[child.Move] = Math.Max(child.Visits / root.Visits, MinimumPolicyValue);
        }

        // if the policy is all zero then dont store it because it means no moves are posible from this node
        if (policy.Sum() == 0)
        {
            return;
        }

        telemetryHistory.StoreTempData(root.GameBoard, policy);
    }

    private Node FindRootNode(GameBoard gameBoard, int previousPlayer)
    {
        List<Node> children = _rootNode?.Children ?? [];
        foreach (var childNode in children)
        {
            if (childNode.GameBoard.StateToString() == gameBoard.StateToString())
            {
                return childNode;
            }
        }

        return new Node(gameBoard.Copy(), previousPlayer);
    }
}