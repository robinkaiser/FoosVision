// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Common.Types;

namespace FoosVision.Common.UnitTests.Types;

public class ResultTests
{
    private enum TestError
    {
        None = 0,
        NotFound = 1,
        Invalid = 2,
    }

    [Fact]
    public void Construct_success_result()
    {
        var r = Result<int, TestError>.Success(42);

        Assert.True(r.IsSuccess);
        Assert.False(r.IsFailure);
        Assert.Equal(42, r.Value);
    }

    [Fact]
    public void Construct_error_result()
    {
        var r = Result<int, TestError>.Failure(TestError.NotFound);

        Assert.False(r.IsSuccess);
        Assert.True(r.IsFailure);
        Assert.Equal(TestError.NotFound, r.Error);
    }

    [Fact]
    public void Construct_implicit_from_value()
    {
        Result<string, TestError> r = "ok";

        Assert.True(r.IsSuccess);
        Assert.Equal("ok", r.Value);
    }

    [Fact]
    public void Construct_implicit_from_error()
    {
        Result<string, TestError> r = TestError.Invalid;

        Assert.True(r.IsFailure);
        Assert.Equal(TestError.Invalid, r.Error);
    }

    [Fact]
    public void Access_value_on_failure_throws()
    {
        var r = Result<int, TestError>.Failure(TestError.Invalid);

        var ex = Assert.Throws<InvalidOperationException>(() => _ = r.Value);
    }

    [Fact]
    public void Access_error_on_success_throws()
    {
        var r = Result<int, TestError>.Success(42);

        var ex = Assert.Throws<InvalidOperationException>(() => _ = r.Error);
    }

    [Fact]
    public void Failure_with_default_enum_is_allowed()
    {
        var r = Result<int, TestError>.Failure(default);

        Assert.True(r.IsFailure);
        Assert.Equal(TestError.None, r.Error);
    }

    [Fact]
    public void Match_on_success()
    {
        Result<string, TestError> r = "abc";

        int length = r.Match(
            v => v.Length,
            e => throw new Exception("should not be called"));

        Assert.Equal(3, length);
    }

    [Fact]
    public void Match_on_failure()
    {
        Result<string, TestError> r = TestError.Invalid;

        string msg = r.Match(
            v => throw new Exception("should not be called"),
            e => $"ERR:{e}");

        Assert.Equal("ERR:Invalid", msg);
    }

    [Fact]
    public void Switch_on_success()
    {
        Result<int, TestError> r = 5;

        int successCalls = 0;
        int failureCalls = 0;

        r.Switch(
            v => successCalls++,
            e => failureCalls++);

        Assert.Equal(1, successCalls);
        Assert.Equal(0, failureCalls);
    }

    [Fact]
    public void Switch_on_failure()
    {
        Result<int, TestError> r = TestError.NotFound;

        int successCalls = 0;
        int failureCalls = 0;

        r.Switch(
            V => successCalls++,
            e => failureCalls++);

        Assert.Equal(0, successCalls);
        Assert.Equal(1, failureCalls);
    }

    [Fact]
    public void Map_on_success()
    {
        Result<string, TestError> r = "abcd";

        var mapped = r.Map(s => s.Length);

        Assert.True(mapped.IsSuccess);
        Assert.Equal(4, mapped.Value);
    }

    [Fact]
    public void Map_on_failure()
    {
        Result<string, TestError> r = TestError.Invalid;

        var mapped = r.Map(s => s.Length);

        Assert.True(mapped.IsFailure);
        Assert.Equal(TestError.Invalid, mapped.Error);
    }

    [Fact]
    public void Bind_on_success_invokes_binder()
    {
        Result<int, TestError> r = 6;

        var bound = r.Bind(ToEvenOdd);

        Assert.True(bound.IsSuccess);
        Assert.Equal("even", bound.Value);
    }

    [Fact]
    public void Bind_on_success_propagates_failure()
    {
        Result<int, TestError> r = 3;

        var bound = r.Bind(ToEvenOdd);

        Assert.True(bound.IsFailure);
        Assert.Equal(TestError.Invalid, bound.Error);
    }

    [Fact]
    public void Bind_on_failure_skips_binder()
    {
        Result<int, TestError> r = TestError.NotFound;

        int calls = 0;
        var bound = r.Bind(_ =>
        {
            calls++;
            return Result<string, TestError>.Success("won't happen");
        });

        Assert.True(bound.IsFailure);
        Assert.Equal(TestError.NotFound, bound.Error);
        Assert.Equal(0, calls);
    }

    [Fact]
    public void Value_then_map_then_bind_roundtrip()
    {
        Result<int, TestError> r = 10;

        var length = r
            .Map(v => v.ToString())          // "10"
            .Map(s => $"#{s}#")              // "#10#"
            .Bind(s => Result<int, TestError>.Success(s.Length)); // 4

        Assert.True(length.IsSuccess);
        Assert.Equal(4, length.Value);
    }

    private static Result<string, TestError> ToEvenOdd(int n) =>
     (n % 2 == 0)
         ? Result<string, TestError>.Success("even")
         : Result<string, TestError>.Failure(TestError.Invalid);
}
