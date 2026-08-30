// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using System.Collections;

namespace FoosVision.Vision.TableScene.Processing.Common;

internal class ReadOnlyBuffer<T> : IReadOnlyList<T>
{
    private readonly T[] _Items;

    public ReadOnlyBuffer(T[] items)
    {
        _Items = items;
    }

    public int Count { get; private set; }

    public T this[int index] => index >= 0 && index < Count
        ? _Items[index]
        : throw new ArgumentOutOfRangeException(nameof(index));

    public void SetCount(int count)
    {
        if (count < 0 ||
            count > _Items.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(count));
        }

        Count = count;
    }

    public Enumerator GetEnumerator()
        => new Enumerator(_Items, Count);

    IEnumerator<T> IEnumerable<T>.GetEnumerator()
        => new Enumerator(_Items, Count);

    IEnumerator IEnumerable.GetEnumerator()
        => new Enumerator(_Items, Count);

    public struct Enumerator : IEnumerator<T>
    {
        private readonly T[] _Items;
        private readonly int _Count;
        private int _Index;

        public Enumerator(T[] items, int count)
        {
            _Items = items;
            _Count = count;
            _Index = -1;
        }

        public T Current => _Items[_Index];

        object? IEnumerator.Current => Current;

        public bool MoveNext()
        {
            _Index++;

            return _Index < _Count;
        }

        public void Reset()
            => _Index = -1;

        public void Dispose()
        {
        }
    }
}
