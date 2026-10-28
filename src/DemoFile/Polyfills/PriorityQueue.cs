namespace System.Collections.Generic;

/// <summary>
/// Port of the .NET 6+ <c>PriorityQueue</c> (4-ary min-heap) for netstandard2.1.
/// Only the members used by DemoFile are included. The heap algorithm matches the
/// runtime implementation so elements with equal priorities dequeue in the same order.
/// </summary>
internal class PriorityQueue<TElement, TPriority>
{
    private const int Arity = 4;
    private const int Log2Arity = 2;

    private (TElement Element, TPriority Priority)[] _nodes;
    private int _size;

    public PriorityQueue()
    {
        _nodes = Array.Empty<(TElement, TPriority)>();
    }

    public PriorityQueue(int initialCapacity)
    {
        if (initialCapacity < 0)
            throw new ArgumentOutOfRangeException(nameof(initialCapacity));

        _nodes = new (TElement, TPriority)[initialCapacity];
    }

    public int Count => _size;

    public void Enqueue(TElement element, TPriority priority)
    {
        var currentSize = _size;
        if (_nodes.Length == currentSize)
            Grow(currentSize + 1);

        _size = currentSize + 1;
        MoveUp((element, priority), currentSize);
    }

    public TElement Dequeue()
    {
        if (_size == 0)
            throw new InvalidOperationException("Queue empty.");

        var element = _nodes[0].Element;
        RemoveRootNode();
        return element;
    }

    public bool TryDequeue(out TElement element, out TPriority priority)
    {
        if (_size != 0)
        {
            (element, priority) = _nodes[0];
            RemoveRootNode();
            return true;
        }

        element = default!;
        priority = default!;
        return false;
    }

    public bool TryPeek(out TElement element, out TPriority priority)
    {
        if (_size != 0)
        {
            (element, priority) = _nodes[0];
            return true;
        }

        element = default!;
        priority = default!;
        return false;
    }

    public void Clear()
    {
        Array.Clear(_nodes, 0, _size);
        _size = 0;
    }

    private void Grow(int minCapacity)
    {
        const int growFactor = 2;
        const int minimumGrow = 4;
        const int maxArrayLength = 0x7FFFFFC7;

        var newCapacity = growFactor * _nodes.Length;
        if ((uint)newCapacity > maxArrayLength)
            newCapacity = maxArrayLength;

        newCapacity = Math.Max(newCapacity, _nodes.Length + minimumGrow);
        if (newCapacity < minCapacity)
            newCapacity = minCapacity;

        Array.Resize(ref _nodes, newCapacity);
    }

    private void RemoveRootNode()
    {
        var lastNodeIndex = --_size;
        if (lastNodeIndex > 0)
        {
            var lastNode = _nodes[lastNodeIndex];
            MoveDown(lastNode, 0);
        }

        _nodes[lastNodeIndex] = default!;
    }

    private static int GetParentIndex(int index) => (index - 1) >> Log2Arity;

    private static int GetFirstChildIndex(int index) => (index << Log2Arity) + 1;

    private void MoveUp((TElement Element, TPriority Priority) node, int nodeIndex)
    {
        var nodes = _nodes;
        var comparer = Comparer<TPriority>.Default;

        while (nodeIndex > 0)
        {
            var parentIndex = GetParentIndex(nodeIndex);
            var parent = nodes[parentIndex];

            if (comparer.Compare(node.Priority, parent.Priority) < 0)
            {
                nodes[nodeIndex] = parent;
                nodeIndex = parentIndex;
            }
            else
            {
                break;
            }
        }

        nodes[nodeIndex] = node;
    }

    private void MoveDown((TElement Element, TPriority Priority) node, int nodeIndex)
    {
        var nodes = _nodes;
        var size = _size;
        var comparer = Comparer<TPriority>.Default;

        int i;
        while ((i = GetFirstChildIndex(nodeIndex)) < size)
        {
            var minChild = nodes[i];
            var minChildIndex = i;

            var childIndexUpperBound = Math.Min(i + Arity, size);
            while (++i < childIndexUpperBound)
            {
                var nextChild = nodes[i];
                if (comparer.Compare(nextChild.Priority, minChild.Priority) < 0)
                {
                    minChild = nextChild;
                    minChildIndex = i;
                }
            }

            if (comparer.Compare(node.Priority, minChild.Priority) <= 0)
                break;

            nodes[nodeIndex] = minChild;
            nodeIndex = minChildIndex;
        }

        nodes[nodeIndex] = node;
    }
}
