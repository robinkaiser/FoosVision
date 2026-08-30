// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

namespace FoosVision.Common.Logging;

public class Source
{
    public string Name { get; }

    public Source(string name) => Name = name;

    public virtual void Write(
        Severity severity,
        string messageTemplate,
        params object?[] args)
    {
        if (severity < LogControl.MinimumSeverity) return;

        Logger.Write(
            source: Name,
            severity: severity,
            messageTemplate: messageTemplate,
            args: args ?? []);
    }

    public void Verbose(string template) => Write(Severity.Verbose, template);
    public void Verbose(string template, params object?[] args) => Write(Severity.Verbose, template, args);

    public void Debug(string template) => Write(Severity.Debug, template);
    public void Debug(string template, params object?[] args) => Write(Severity.Debug, template, args);

    public void Information(string template) => Write(Severity.Information, template);
    public void Information(string template, params object?[] args) => Write(Severity.Information, template, args);

    public void Warning(string template) => Write(Severity.Warning, template);
    public void Warning(string template, params object?[] args) => Write(Severity.Warning, template, args);

    public void Error(string template) => Write(Severity.Error, template);
    public void Error(string template, params object?[] args) => Write(Severity.Error, template, args);

    public void Fatal(string template) => Write(Severity.Fatal, template);
    public void Fatal(string template, params object?[] args) => Write(Severity.Fatal, template, args);
}
