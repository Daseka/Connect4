using DeepNetwork.NetworkIO;

namespace Connect4.Ais;

public static class AgentExtensions
{
    public static Mcts ToMctsCloned(this Agent agent, int maxIterations, Random? random = null)
    {
        return new Mcts(maxIterations, agent.ValueNetwork?.Clone(), agent.PolicyNetwork?.Clone(), random);
    }

    public static Mcts ToMcts(this Agent agent, int maxIterations, Random? random = null)
    {
        return new Mcts(maxIterations, agent.ValueNetwork, agent.PolicyNetwork, random);
    }
}
