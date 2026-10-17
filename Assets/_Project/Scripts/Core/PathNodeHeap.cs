namespace StrategyDemo.Core
{
    /// <summary>
    /// A*'s open list: a binary min-heap of grid node indices ordered by <c>f</c>, ties broken by the smaller
    /// <c>h</c> (the node closer to the goal goes first, so equal-cost paths are explored straight towards it).
    /// </summary>
    /// <remarks>
    /// A binary heap is an array read as a tree: the children of index i sit at 2i+1 and 2i+2, and every parent is
    /// smaller than its children, so the best node is always at index 0. Push and Pop move one node up or down a
    /// single branch, O(log n), where scanning a plain list for the minimum would be O(n).
    /// <see cref="_positions"/> remembers where each node sits, so a node whose cost improved can be moved up
    /// without searching for it (the "decrease key" step). All arrays are sized once; nothing is allocated per search.
    /// </remarks>
    internal sealed class PathNodeHeap
    {
        // _nodes[0.._count) is the heap itself.
        private readonly int[] _nodes;
        // Node → its index in _nodes; valid only for nodes in the heap, which A* knows: reached but not closed.
        private readonly int[] _positions;
        private readonly int[] _f;
        private readonly int[] _h;

        /// <param name="capacity">Number of distinct nodes (grid cells); each can be in the heap at most once.</param>
        public PathNodeHeap(int capacity)
        {
            _nodes = new int[capacity];
            _positions = new int[capacity];
            _f = new int[capacity];
            _h = new int[capacity];
        }

        /// <summary>Nodes currently in the heap.</summary>
        public int Count { get; private set; }

        /// <summary>Empties the heap in O(1); the arrays are reused by the next search.</summary>
        public void Clear()
        {
            Count = 0;
        }

        /// <summary>Adds <paramref name="node"/>, which must not be in the heap yet.</summary>
        public void Push(int node, int f, int h)
        {
            _f[node] = f;
            _h[node] = h;
            Place(node, Count);
            Count++;
            SiftUp(Count - 1);
        }

        /// <summary>
        /// Lowers the <c>f</c> of a node that is in the heap (a cheaper way to reach it was found). The caller knows it
        /// is there: in A* a node reached in this search and not yet closed is always in the open list.
        /// </summary>
        public void DecreaseF(int node, int f)
        {
            _f[node] = f;
            SiftUp(_positions[node]);
        }

        /// <summary>Removes and returns the node with the smallest <c>f</c> (then smallest <c>h</c>).</summary>
        public int Pop()
        {
            int best = _nodes[0];
            Count--;
            if (Count > 0)
            {
                // Move the last leaf to the root and let it sink to where it belongs.
                Place(_nodes[Count], 0);
                SiftDown(0);
            }

            return best;
        }

        private void SiftUp(int index)
        {
            int node = _nodes[index];
            while (index > 0)
            {
                int parent = (index - 1) / 2;
                if (!IsBetter(node, _nodes[parent]))
                {
                    break;
                }

                Place(_nodes[parent], index);
                index = parent;
            }

            Place(node, index);
        }

        private void SiftDown(int index)
        {
            int node = _nodes[index];
            while (true)
            {
                int child = 2 * index + 1;
                if (child >= Count)
                {
                    break;
                }

                // Follow the better of the two children; the right one exists only if it is still inside the heap.
                if (child + 1 < Count && IsBetter(_nodes[child + 1], _nodes[child]))
                {
                    child++;
                }

                if (!IsBetter(_nodes[child], node))
                {
                    break;
                }

                Place(_nodes[child], index);
                index = child;
            }

            Place(node, index);
        }

        private bool IsBetter(int a, int b)
        {
            return _f[a] < _f[b] || (_f[a] == _f[b] && _h[a] < _h[b]);
        }

        private void Place(int node, int index)
        {
            _nodes[index] = node;
            _positions[node] = index;
        }
    }
}
