// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Common.Types;

namespace FoosVision.Common.UnitTests.Types;

public class OptionTests
{
    [Fact]
    public void Construct_some()
    {
        var o = Option<int>.Some(42);

        Assert.True(o.HasValue);
        Assert.True(o.IsSome);
        Assert.False(o.IsNone);
        Assert.Equal(42, o.Value);
    }

    [Fact]
    public void Construct_none()
    {
        var o = Option<int>.None();

        Assert.False(o.HasValue);
        Assert.False(o.IsSome);
        Assert.True(o.IsNone);
    }

    [Fact]
    public void Some_with_null_throws()
    {
        var ex = Assert.Throws<ArgumentNullException>(() => Option<string>.Some(null!));
        Assert.Equal("value", ex.ParamName);
    }

    [Fact]
    public void Implicit_from_value_creates_some()
    {
        Option<string> o = "abc";

        Assert.True(o.IsSome);
        Assert.False(o.IsNone);
        Assert.Equal("abc", o.Value);
    }

    [Fact]
    public void Implicit_from_null_creates_none()
    {
        string? value = null;

        Option<string> o = value!;

        Assert.False(o.IsSome);
        Assert.True(o.IsNone);
    }

    [Fact]
    public void None_value_access_throws()
    {
        var o = Option<int>.None();

        var ex = Assert.Throws<InvalidOperationException>(() => _ = o.Value);
        Assert.Equal("No value present.", ex.Message);
    }

    [Fact]
    public void TryGetValue_on_some_returns_true_and_sets_value()
    {
        Option<string> o = Option<string>.Some("abc");

        var ok = o.TryGetValue(out var value);

        Assert.True(ok);
        Assert.Equal("abc", value);
    }

    [Fact]
    public void TryGetValue_on_none_returns_false_and_sets_default()
    {
        var o = Option<string>.None();

        var ok = o.TryGetValue(out var value);

        Assert.False(ok);
        Assert.Null(value); // default for string is null
    }

    [Fact]
    public void TryGetValue_on_some_allows_safe_use_without_Value_property()
    {
        Option<int> o = 42;

        if (!o.TryGetValue(out var value))
            throw new Exception("should not happen");

        Assert.Equal(42, value);
    }

    [Fact]
    public void TryGetValue_on_none_sets_default_for_value_types()
    {
        var o = Option<int>.None();

        var ok = o.TryGetValue(out var value);

        Assert.False(ok);
        Assert.Equal(0, value); // default for int
    }

    [Fact]
    public void TryGetValue_on_none_does_not_throw()
    {
        var o = Option<int>.None();

        var ex = Record.Exception(() => o.TryGetValue(out _));

        Assert.Null(ex);
    }

    [Fact]
    public void Match_on_some()
    {
        Option<string> o = Option<string>.Some("abc");

        var result = o.Match(
            some => some.Length,
            () => throw new Exception("should not be called"));

        Assert.Equal(3, result);
    }

    [Fact]
    public void Match_on_none()
    {
        var o = Option<string>.None();

        var result = o.Match(
            some => throw new Exception("should not be called"),
            () => 123);

        Assert.Equal(123, result);
    }

    [Fact]
    public void Switch_on_some()
    {
        Option<int> o = 5;

        int someCalls = 0;
        int noneCalls = 0;

        o.Switch(
            v => someCalls++,
            () => noneCalls++);

        Assert.Equal(1, someCalls);
        Assert.Equal(0, noneCalls);
    }

    [Fact]
    public void Switch_on_none()
    {
        var o = Option<int>.None();

        int someCalls = 0;
        int noneCalls = 0;

        o.Switch(
            v => someCalls++,
            () => noneCalls++);

        Assert.Equal(0, someCalls);
        Assert.Equal(1, noneCalls);
    }

    [Fact]
    public void Map_on_some()
    {
        Option<string> o = "abcd";

        var mapped = o.Map(s => s.Length);

        Assert.True(mapped.IsSome);
        Assert.False(mapped.IsNone);
        Assert.Equal(4, mapped.Value);
    }

    [Fact]
    public void Map_on_none()
    {
        var o = Option<string>.None();

        var mapped = o.Map(s => s.Length);

        Assert.False(mapped.IsSome);
        Assert.True(mapped.IsNone);
    }

    [Fact]
    public void Bind_on_some_invokes_binder()
    {
        Option<int> o = 10;

        var bound = o.Bind(ToNonEmptyString);

        Assert.True(bound.IsSome);
        Assert.Equal("10", bound.Value);
    }

    [Fact]
    public void Bind_on_some_propagates_none_from_binder()
    {
        Option<int> o = 0;

        var bound = o.Bind(ToNonEmptyString);

        Assert.True(bound.IsNone);
        Assert.False(bound.IsSome);
    }

    [Fact]
    public void Bind_on_none_skips_binder()
    {
        var o = Option<int>.None();

        int calls = 0;
        var bound = o.Bind(v =>
        {
            calls++;
            return Option<string>.Some("won't happen");
        });

        Assert.True(bound.IsNone);
        Assert.Equal(0, calls);
    }

    [Fact]
    public void GetValueOrDefault_on_some_returns_value()
    {
        Option<int> o = 5;

        var v = o.GetValueOrDefault(99);

        Assert.Equal(5, v);
    }

    [Fact]
    public void GetValueOrDefault_on_none_returns_default()
    {
        var o = Option<int>.None();

        var v = o.GetValueOrDefault(99);

        Assert.Equal(99, v);
    }

    [Fact]
    public void GetValueOrDefault_on_none_with_type_default()
    {
        var o = Option<int>.None();

        var v = o.GetValueOrDefault();

        Assert.Equal(0, v);
    }

    [Fact]
    public void Chained_Map_and_Bind_roundtrip()
    {
        Option<int> o = 10;

        var final = o
            .Map(v => v.ToString())             // "10"
            .Map(s => $"#{s}#")                 // "#10#"
            .Bind(s => Option<int>.Some(s.Length)); // 4

        Assert.True(final.IsSome);
        Assert.Equal(4, final.Value);
    }

    private static Option<string> ToNonEmptyString(int n)
        => n == 0
            ? Option<string>.None()
            : Option<string>.Some(n.ToString());
}
