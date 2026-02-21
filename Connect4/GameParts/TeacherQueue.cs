namespace Connect4.GameParts;

[Serializable]
public class TeacherQueue<T>
{
    private const int MaxCapacity = 5;
    private readonly Queue<T> _queue = new();
    public int Count => _queue.Count;

    public void Enqueue(T item)
    {
        _queue.Enqueue(item);

        while (_queue.Count > MaxCapacity)
        {
            _ = _queue.Dequeue();
        }
    }

    public T? Peek()
    {
        return _queue.Count == 0
            ? default
            : _queue.Peek();
    }

    public T? Dequeue()
    {
        return _queue.Count == 0
            ? default
            : _queue.Dequeue();
    }

    public T[] ToArray()
    {
        return [.. _queue];
    }
}
