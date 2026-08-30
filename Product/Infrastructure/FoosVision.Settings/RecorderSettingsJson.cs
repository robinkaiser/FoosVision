// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using System.Text.Json;
using System.Text.Json.Serialization;

namespace FoosVision.Settings;

public static class RecorderSettingsJson
{
    private static readonly JsonSerializerOptions _JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        WriteIndented = true,
    };

    public static RecorderSettings DeserializeAndValidate(string json)
    {
        RecorderSettings? settings = JsonSerializer.Deserialize<RecorderSettings>(json, _JsonOptions);

        if (settings is null)
        {
            throw new JsonException("Settings config is empty.");
        }

        settings.Validate();
        return settings;
    }

    public static string Serialize(RecorderSettings settings)
    {
        return JsonSerializer.Serialize(settings, _JsonOptions);
    }
}
