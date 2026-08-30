// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using Android.Content;
using FoosVision.Adapters.Viewer.Session.Playback;

namespace FoosVision.Viewer.App.Platforms.Android.Screen.Stage;

public class WritableSessionFile : IWritableSessionFile
{
    private readonly string _Path;

    public WritableSessionFile(Context context, string fileName)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);

        _Path = global::System.IO.Path.Combine(context.CacheDir!.AbsolutePath!, fileName);
    }

    public string Path => _Path;

    public void WriteAllText(string content)
    {
        File.WriteAllText(_Path, content);
    }
}
