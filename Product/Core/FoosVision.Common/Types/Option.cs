// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using System.Diagnostics.CodeAnalysis;

namespace FoosVision.Common.Types;

public class Option<T>
{
    private static readonly Option<T> _None = new(false, default!);
    private readonly T _Value;

    private Option(bool hasValue, T value)
    {
        HasValue = hasValue;
        _Value = value!;
    }

    public bool HasValue { get; }

    public bool IsSome => HasValue;

    public bool IsNone => !HasValue;

    public T Value => HasValue
        ? _Value
        : throw new InvalidOperationException("No value present.");

    public static Option<T> Some(T value)
        => value is null
            ? throw new ArgumentNullException(nameof(value))
            : new Option<T>(true, value);

    public static Option<T> None() => _None;

    public bool TryGetValue([NotNullWhen(true)] out T value)
    {
        if (HasValue)
        {
            value = _Value!;
            return true;
        }

        value = default!;
        return false;
    }

    public TResult Match<TResult>(Func<T, TResult> some, Func<TResult> none)
        => HasValue ? some(_Value) : none();

    public void Switch(Action<T> some, Action none)
    {
        if (HasValue)
        {
            some(_Value);
        }
        else
        {
            none();
        }
    }

    public Option<TOut> Map<TOut>(Func<T, TOut> map)
        => HasValue ? Option<TOut>.Some(map(_Value)) : Option<TOut>.None();

    public Option<TOut> Bind<TOut>(Func<T, Option<TOut>> bind)
        => HasValue ? bind(_Value) : Option<TOut>.None();

    public T GetValueOrDefault(T defaultValue = default!)
        => HasValue ? _Value : defaultValue;

    public static implicit operator Option<T>(T value)
        => value is null ? None() : Some(value);
}
