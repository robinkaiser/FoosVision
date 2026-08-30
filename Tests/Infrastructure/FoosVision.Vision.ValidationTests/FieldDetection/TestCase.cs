// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using Xunit.Sdk;

namespace FoosVision.Vision.ValidationTests.FieldDetection;

public class TestCase : IXunitSerializable
{
    public TestCase()
    {
    }

    public TestCase(string pngPath, string jsonPath)
    {
        PngPath = pngPath;
        JsonPath = jsonPath;
    }

    public string PngPath { get; private set; } = string.Empty;

    public string JsonPath { get; private set; } = string.Empty;

    public void Deserialize(IXunitSerializationInfo info)
    {
        PngPath = info.GetValue<string>(nameof(PngPath))!;
        JsonPath = info.GetValue<string>(nameof(JsonPath))!;
    }

    public void Serialize(IXunitSerializationInfo info)
    {
        info.AddValue(nameof(PngPath), PngPath);
        info.AddValue(nameof(JsonPath), JsonPath);
    }

    public override string ToString()
    {
        return Path.GetFileNameWithoutExtension(PngPath);
    }
}
