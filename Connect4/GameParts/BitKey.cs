namespace Connect4.GameParts;

[Serializable]
public struct BitKey : IEquatable<BitKey>
{
    public ulong High;
    public ulong Low;
    private const int EndHigh = 128;
    private const int EndLow = 64;
    /* BoardSize = 3 times the board size of 42 
     * empty, red player, yellow player, + 1 for 
     * who played to create this boardstate*/
    private const int BoardSize = 127; 

    public static int[] ToArray(string key)
    {
        string[] parts = key.Split(',');
        if (parts.Length != 2 || !ulong.TryParse(parts[0], out ulong low) || !ulong.TryParse(parts[1], out ulong high))
        {
            throw new ArgumentException("Invalid key format.");
        }

        int[] boardState = new int[BoardSize];
        for (int i = 0; i < EndLow; i++)
        {
            if ((low & (1UL << i)) != 0)
            {
                boardState[i] = 1;
            }
        }

        for (int i = EndLow; i < BoardSize; i++)
        {
            if ((high & (1UL << (i - EndLow))) != 0)
            {
                boardState[i] = 1;
            }
        }

        return boardState;
    }

    public static string ToKey(int[] boardState)
    {
        ulong low = 0, high = 0;
        for (int i = 0; i < boardState.Length && i < EndLow; i++)
        {
            if (boardState[i] != 0)
            {
                low |= 1UL << i;
            }
        }

        for (int i = EndLow; i < boardState.Length && i < EndHigh; i++)
        {
            if (boardState[i] != 0)
            {
                high |= 1UL << (i - EndLow);
            }
        }

        return $"{low},{high}";
    }

    public static bool operator !=(BitKey left, BitKey right)
    {
        return !(left == right);
    }

    public static bool operator ==(BitKey left, BitKey right)
    {
        return left.Equals(right);
    }

    public readonly bool Equals(BitKey other)
    {
        return Low == other.Low && High == other.High;
    }

    public override readonly bool Equals(object? obj)
    {
        return obj is BitKey other && Equals(other);
    }

    public override readonly int GetHashCode()
    {
        return HashCode.Combine(Low, High);
    }
}
