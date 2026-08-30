// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

namespace FoosVision.Common.Types;

public class Result<TValue, TError>
    where TError : Enum
{
    private readonly TValue _Value;
    private readonly TError _Error;

    private Result(TValue value)
    {
        IsSuccess = true;
        _Value = value;
        _Error = default!;
    }

    private Result(TError error)
    {
        IsSuccess = false;
        _Value = default!;
        _Error = error;
    }

    public bool IsSuccess { get; }

    public bool IsFailure => !IsSuccess;

    public TValue Value => IsSuccess
        ? _Value
        : throw new InvalidOperationException("No value for a failed result.");

    public TError Error => IsSuccess
        ? throw new InvalidOperationException("No error for a successful result.")
        : _Error;

    public static implicit operator Result<TValue, TError>(TValue value) => new(value);

    public static implicit operator Result<TValue, TError>(TError error) => new(error);

    public static Result<TValue, TError> Success(TValue value)
        => new(value);

    public static Result<TValue, TError> Failure(TError error)
        => new(error);

    public TResult Match<TResult>(Func<TValue, TResult> success, Func<TError, TResult> failure)
        => IsSuccess ? success(_Value) :
                       failure(_Error);

    public void Switch(Action<TValue> success, Action<TError> failure)
    {
        if (IsSuccess)
        {
            success(_Value);
        }
        else
        {
            failure(_Error);
        }
    }

    public Result<TOut, TError> Map<TOut>(Func<TValue, TOut> map)
        => IsSuccess ? Result<TOut, TError>.Success(map(_Value))
                     : Result<TOut, TError>.Failure(_Error);

    public Result<TOut, TError> Bind<TOut>(Func<TValue, Result<TOut, TError>> bind)
        => IsSuccess ? bind(_Value) :
                       Result<TOut, TError>.Failure(_Error);
}
