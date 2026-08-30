// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using Xunit.Sdk;

namespace FoosVision.Vision.ValidationTests.TableScene;

public class TestCase : IXunitSerializable
{
    public TestCase()
    {
    }

    public TestCase(string pngPath)
    {
        PngPath = pngPath;
    }

    public string PngPath { get; private set; } = string.Empty;

    public void Deserialize(IXunitSerializationInfo info)
    {
        PngPath = info.GetValue<string>(nameof(PngPath))!;
    }

    public void Serialize(IXunitSerializationInfo info)
    {
        info.AddValue(nameof(PngPath), PngPath);
    }

    public override string ToString()
    {
        return Path.GetFileNameWithoutExtension(PngPath);
    }
}
