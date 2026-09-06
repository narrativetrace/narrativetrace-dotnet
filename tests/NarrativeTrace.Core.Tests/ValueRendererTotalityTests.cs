// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Core;
using Xunit;

namespace NarrativeTrace.Core.Tests;

/// <summary>
/// No-poison contract (port-agnostic invariant): <see cref="ValueRenderer"/>
/// is total — nothing a hostile value does (throwing enumerator, throwing
/// <c>Count</c>, throwing <c>Exception.Message</c>, a throwing reflected
/// <c>Task&lt;T&gt;.Result</c>) may escape <see cref="ValueRenderer.Render"/>
/// or <see cref="ValueRenderer.RenderStructured"/>, and one bad element must
/// degrade only that element's rendering, not the whole render. Mirrors
/// Java's <c>NoPoisonContractTest</c> shape, adapted to this port's
/// enumerable/dictionary/reflection idioms.
/// </summary>
public class ValueRendererTotalityTests
{
    [Fact]
    public void Throwing_GetEnumerator_does_not_escape_flat_render()
    {
        // Nothing was collected before GetEnumerator itself failed, so the
        // whole (root) value degrades to the same "<TypeName>" placeholder
        // a throwing ToString() would — not a misleading "[]" or "<error>".
        var result = ValueRenderer.Render(new ThrowingGetEnumerator());

        Assert.Equal("<ThrowingGetEnumerator>", result);
    }

    [Fact]
    public void Throwing_GetEnumerator_does_not_escape_structured_render()
    {
        var result = ValueRenderer.RenderStructured(new ThrowingGetEnumerator());

        Assert.IsType<RenderedValue.StringVal>(result);
    }

    [Fact]
    public void Throwing_MoveNext_mid_iteration_keeps_items_collected_so_far()
    {
        var seq = new ThrowingMoveNextAt(index: 2, total: 5);

        var result = ValueRenderer.Render(seq);

        Assert.Contains("0", result);
        Assert.Contains("1", result);
        Assert.DoesNotContain("4", result);
    }

    [Fact]
    public void Throwing_Current_degrades_only_that_element_iteration_continues()
    {
        var seq = new ThrowingCurrentAt(index: 1, total: 3);

        var result = ValueRenderer.Render(seq);

        Assert.Contains("0", result);
        Assert.Contains("<error>", result);
        Assert.Contains("2", result);
    }

    [Fact]
    public void Throwing_Current_in_structured_render_degrades_only_that_element()
    {
        var seq = new ThrowingCurrentAt(index: 1, total: 3);

        var result = ValueRenderer.RenderStructured(seq);

        var list = Assert.IsType<RenderedValue.ListVal>(result);
        Assert.Equal(3, list.Elements.Count);
        Assert.IsType<RenderedValue.StringVal>(list.Elements[1]);
    }

    [Fact]
    public void Throwing_dictionary_Count_does_not_escape_render()
    {
        var dict = new ThrowingCountDictionary();
        dict.Add("a", "1");
        dict.Add("b", "2");

        var result = ValueRenderer.Render(dict);

        Assert.Contains("a=\"1\"", result);
        Assert.Contains("b=\"2\"", result);
    }

    [Fact]
    public void Throwing_dictionary_enumeration_does_not_escape_structured_render()
    {
        var dict = new ThrowingCurrentDictionary();
        dict.Add("a", "1");

        var result = ValueRenderer.RenderStructured(dict);

        Assert.IsType<RenderedValue.ObjectVal>(result);
    }

    [Fact]
    public void Exception_subclass_that_throws_from_Message_still_renders()
    {
        var ex = new ThrowingMessageException();

        var result = ValueRenderer.Render(ex);

        Assert.Contains(nameof(ThrowingMessageException), result);
        Assert.DoesNotContain("boom-from-message", result);
    }

    [Fact]
    public void Completed_task_whose_reflected_Result_throws_does_not_escape()
    {
        var task = new HostileResultTask<int>(() => 0);
        task.RunSynchronously();

        var result = ValueRenderer.Render(task);

        Assert.Equal("<error>", result);
    }

    [Fact]
    public void Completed_task_whose_reflected_Result_throws_does_not_escape_structured()
    {
        var task = new HostileResultTask<int>(() => 0);
        task.RunSynchronously();

        var result = ValueRenderer.RenderStructured(task);

        Assert.IsType<RenderedValue.StringVal>(result);
    }

    [Fact]
    public void One_bad_element_in_a_large_list_degrades_only_that_element()
    {
        var seq = new List<object> { "a", new ThrowingToStringValue(), "c" };

        var result = ValueRenderer.Render(seq);

        Assert.Contains("\"a\"", result);
        Assert.Contains("\"c\"", result);
        Assert.Contains("<ThrowingToStringValue>", result);
    }

    private sealed class ThrowingGetEnumerator : System.Collections.IEnumerable
    {
        public System.Collections.IEnumerator GetEnumerator() =>
            throw new InvalidOperationException("boom");
    }

    private sealed class ThrowingMoveNextAt(int index, int total)
        : System.Collections.IEnumerable
    {
        public System.Collections.IEnumerator GetEnumerator() =>
            new Enumerator(index, total);

        private sealed class Enumerator(int throwAt, int total)
            : System.Collections.IEnumerator
        {
            private int _position = -1;

            public object Current => _position;

            public bool MoveNext()
            {
                _position++;
                if (_position == throwAt)
                {
                    throw new InvalidOperationException("boom");
                }

                return _position < total;
            }

            public void Reset() => _position = -1;
        }
    }

    private sealed class ThrowingCurrentAt(int index, int total)
        : System.Collections.IEnumerable
    {
        public System.Collections.IEnumerator GetEnumerator() =>
            new Enumerator(index, total);

        private sealed class Enumerator(int throwAt, int total)
            : System.Collections.IEnumerator
        {
            private int _position = -1;

            public object Current => _position == throwAt
                ? throw new InvalidOperationException("boom")
                : _position;

            public bool MoveNext()
            {
                _position++;
                return _position < total;
            }

            public void Reset() => _position = -1;
        }
    }

    private sealed class ThrowingCountDictionary : System.Collections.IDictionary
    {
        private readonly System.Collections.Hashtable _inner = [];

        public int Count => throw new InvalidOperationException("boom");

        public void Add(object key, object? value) => _inner.Add(key, value);

        public System.Collections.IDictionaryEnumerator GetEnumerator() =>
            _inner.GetEnumerator();

        System.Collections.IEnumerator
            System.Collections.IEnumerable.GetEnumerator() =>
            _inner.GetEnumerator();

        public bool IsFixedSize => false;
        public bool IsReadOnly => false;
        public System.Collections.ICollection Keys => _inner.Keys;
        public System.Collections.ICollection Values => _inner.Values;
        public bool IsSynchronized => false;
        public object SyncRoot => this;

        public object? this[object key]
        {
            get => _inner[key];
            set => _inner[key] = value;
        }

        public void Clear() => _inner.Clear();
        public bool Contains(object key) => _inner.Contains(key);
        public void CopyTo(Array array, int index) => _inner.CopyTo(array, index);
        public void Remove(object key) => _inner.Remove(key);
    }

    private sealed class ThrowingCurrentDictionary : System.Collections.IDictionary
    {
        private readonly System.Collections.Hashtable _inner = [];

        public int Count => _inner.Count;

        public void Add(object key, object? value) => _inner.Add(key, value);

        public System.Collections.IDictionaryEnumerator GetEnumerator() =>
            new ThrowingEnumerator(_inner.GetEnumerator());

        System.Collections.IEnumerator
            System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();

        public bool IsFixedSize => false;
        public bool IsReadOnly => false;
        public System.Collections.ICollection Keys => _inner.Keys;
        public System.Collections.ICollection Values => _inner.Values;
        public bool IsSynchronized => false;
        public object SyncRoot => this;

        public object? this[object key]
        {
            get => _inner[key];
            set => _inner[key] = value;
        }

        public void Clear() => _inner.Clear();
        public bool Contains(object key) => _inner.Contains(key);
        public void CopyTo(Array array, int index) => _inner.CopyTo(array, index);
        public void Remove(object key) => _inner.Remove(key);

        private sealed class ThrowingEnumerator(
            System.Collections.IDictionaryEnumerator inner)
            : System.Collections.IDictionaryEnumerator
        {
            public object Key => throw new InvalidOperationException("boom");
            public object? Value => throw new InvalidOperationException("boom");
            public System.Collections.DictionaryEntry Entry =>
                throw new InvalidOperationException("boom");
            public object Current =>
                throw new InvalidOperationException("boom");

            public bool MoveNext() => inner.MoveNext();
            public void Reset() => inner.Reset();
        }
    }

    // S3877 (the throw is the fixture: a Message override that refuses),
    // S3871 (a private exception is the point — nothing outside this file
    // may ever construct or catch it), S1144 (Result is read only via
    // reflection by the totality test, which static usage analysis can't see).
#pragma warning disable S3877, S3871, S1144
    private sealed class ThrowingMessageException()
        : Exception("unused")
    {
        public override string Message => throw new InvalidOperationException(
            "boom-from-message");
    }

    // Must itself be a generic type (not merely derive from a closed
    // Task&lt;T&gt;) — task.GetType().IsGenericType gates the reflected
    // Result read, and a non-generic subclass of Task&lt;int&gt; reports
    // false there, never reaching the hostile property at all.
    private sealed class HostileResultTask<T>(Func<T> function) : Task<T>(function)
    {
        public new T Result => throw new InvalidOperationException("boom");
    }

    private sealed class ThrowingToStringValue
    {
        public override string ToString() =>
            throw new InvalidOperationException("boom");
    }
#pragma warning restore S3877, S3871, S1144
}
