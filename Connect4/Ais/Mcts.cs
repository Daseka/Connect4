using Connect4.GameParts;
using DeepNetwork;

namespace Connect4.Ais;

public class Mcts(
    int maxIterations,
    SimpleDumbNetwork? valueNetwork = null,
    SimpleDumbNetwork? policyNetwork = null,
    Random? random = null)
{
    private const int MaxColumnCount = 7;
    private const int MaxIterationsRandom = 100;
    private readonly int _maxIterations = maxIterations;
    private readonly Random _random = random ?? new();
    private TelemetryHistory _telemetryHistory = new();

    public SimpleDumbNetwork? PolicyNetwork { get; private set; } = policyNetwork;
    public SimpleDumbNetwork? ValueNetwork { get; private set; } = valueNetwork;

    public int GetBestMoveRandom(GameBoard gameBoard, int previousPlayer)
    {
        var rootNode = new Node(gameBoard.Copy(), previousPlayer);

        for (int i = 0; i < MaxIterationsRandom; i++)
        {
            Node? childNode = Select(rootNode, _random);

            int result = Simulate(childNode, _random);

            Backpropagate(childNode, result);
        }

        UpdateTelemeryHistory(rootNode, _telemetryHistory);

        return rootNode.GetBestChild()?.Move ?? -1;
    }

    public int GetBestMove(GameBoard gameBoard, int previousPlayer)
    {
        var rootNode = new Node(gameBoard.Copy(), previousPlayer);
        bool useNetworks = PolicyNetwork?.Trained == true && ValueNetwork?.Trained == true;

        for (int i = 0; i < _maxIterations; i++)
        {
            
            Node? childNode = useNetworks
                ? Select(rootNode, _random, PolicyNetwork)
                : Select(rootNode, _random);

            int result = useNetworks
                ? Simulate(childNode, _random, ValueNetwork)
                : Simulate(childNode, _random);

            Backpropagate(childNode, result);
        }

        UpdateTelemeryHistory(rootNode, _telemetryHistory);

        return rootNode.GetBestChild(PolicyNetwork)?.Move ?? -1;
    }

    public TelemetryHistory GetTelemetryHistory()
    {
        return _telemetryHistory;
    }

    public void SetWinnerTelemetryHistory(Winner winner)
    {
        _telemetryHistory.StoreWinnerData(winner);
    }

    private static void Backpropagate(Node node, int result)
    {
        Node? currentNode = node;
        while (currentNode != null)
        {
            currentNode.Update(result);

            currentNode = currentNode.Parent;
        }
    }

    private static int DeepSimulation(Node node, SimpleDumbNetwork valueNetwork)
    {
        double[] boardStateArray = [.. node.GameBoard.StateToArray().Select(x => (double)x)];
        double winProbability = valueNetwork.Calculate(boardStateArray).First();

        return winProbability > 0.5
            ? (int)Winner.Red
            : (int)Winner.Yellow;
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

    private static int RandomSimulation(Node node, Random random)
    {
        // Check if the game was won by the selected moved of the node
        int previousPlayer = node.PLayerWhoMadeMove;
        if (node.GameBoard.HasWon(previousPlayer))
        {
            return previousPlayer;
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

        // Return the winner
        return previousPlayer == 1 ? 1 : 2;
    }

    private static Node Select(Node node, Random random)
    {
        if (node.IsTerminal)
        {
            return node;
        }

        if (node.IsLeaf())
        {
            return Expand(node, random);
        }

        Node? bestChild = node.GetBestChild();
        while (bestChild is not null && bestChild.Visits > 0 && !bestChild.IsTerminal)
        {
            bestChild = Select(bestChild, random);
        }

        return bestChild ?? node;
    }

    private static Node Select(Node node, Random random, SimpleDumbNetwork policyNetwork)
    {
        if (node.IsTerminal)
        {
            return node;
        }

        if (node.IsLeaf())
        {
            return Expand(node, random);
        }

        Node? bestChild = node.GetBestChild(policyNetwork);
        while (bestChild is not null && bestChild.Visits > 0 && !bestChild.IsTerminal)
        {
            bestChild = Select(bestChild, random, policyNetwork);
        }

        return bestChild ?? node;
    }

    private static int Simulate(Node node, Random random)
    {
        return RandomSimulation(node, random);
    }

    private static int Simulate(Node node, Random random, SimpleDumbNetwork? valueNetwork)
    {
        return DeepSimulation(node, valueNetwork);
    }

    private static void UpdateTelemeryHistory(Node root, TelemetryHistory telemetryHistory)
    {
        // too many results
        //foreach (Node child in root.Children)
        //{
        //    UpdateTelemeryHistory(child, telemetryHistory);
        //}

        double[] policy = new double[MaxColumnCount];
        foreach (Node child in root.Children)
        {
            policy[child.Move] = child.Visits / root.Visits;
        }

        telemetryHistory.StoreTempData(root.GameBoard, policy);
    }
}