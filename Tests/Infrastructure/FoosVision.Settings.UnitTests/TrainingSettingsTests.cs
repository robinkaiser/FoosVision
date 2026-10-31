// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using System.Text.Json;
using System.Text.Json.Nodes;

namespace FoosVision.Settings.UnitTests;

public class TrainingSettingsTests
{
    [Fact]
    public void Missing_training_is_rejected_and_initializer_preserves_file_with_defaults()
    {
        JsonObject json = DefaultJson();
        json.Remove("training");
        string text = json.ToJsonString();
        JsonException error = Assert.Throws<JsonException>(() => RecorderSettingsJson.DeserializeAndValidate(text));
        Assert.Contains("training", error.Message);

        FakeSettingsFileStore files = new();
        string path = Path.Combine("external", "Config.json");
        files.Files[path] = text;
        RecorderSettingsContext context = new RecorderSettingsInitializer(files).Initialize("external", "fallback");
        Assert.Equal(SettingsConfigSource.DefaultsInvalidConfig, context.ConfigSource);
        Assert.Contains("training", context.ConfigError!);
        Assert.Equal(text, files.Files[path]);
        Assert.Equal(3, context.Settings.Training.EnabledStrategies.Count);
    }

    [Theory]
    [InlineData("snakeShot", "push")]
    [InlineData("pullShot", "long")]
    [InlineData("tickTackPass", "wall")]
    public void Json_and_initializer_load_each_strategy_and_update_example(string id, string variant)
    {
        JsonObject json = DefaultJson();
        json["training"]!["enabledStrategies"] = new JsonArray(JsonValue.Create(id));
        json["training"]![id]!["minWaitSeconds"] = 2;
        json["training"]![id]!["maxWaitSeconds"] = 2;
        json["training"]![id]!["waitDistribution"] = "preferLong";
        json["training"]![id]!["variants"] = new JsonArray(new JsonObject { ["id"] = variant, ["text"] = "Ansage" });
        string text = json.ToJsonString();
        RecorderSettings parsed = RecorderSettingsJson.DeserializeAndValidate(text);
        Assert.Equal([id], parsed.Training.EnabledStrategies);
        TrainingStrategySettings strategy = Strategy(parsed.Training, id);
        Assert.Equal(2, strategy.MinWaitSeconds);
        Assert.Equal(2, strategy.MaxWaitSeconds);
        Assert.Equal("preferLong", strategy.WaitDistribution);
        Assert.Equal("Ansage", Assert.Single(strategy.Variants).Text);

        FakeSettingsFileStore files = new();
        string path = Path.Combine("external", "Config.json");
        files.Files[path] = text;
        RecorderSettingsContext context = new RecorderSettingsInitializer(files).Initialize("external", "fallback");
        Assert.Equal(SettingsConfigSource.ConfigFile, context.ConfigSource);
        Assert.Equal(text, files.Files[path]);
        Assert.Equal([id], context.Settings.Training.EnabledStrategies);
        RecorderSettings example = RecorderSettingsJson.DeserializeAndValidate(files.Files[context.Paths.ExampleConfig]);
        Assert.Equal(3, example.Training.EnabledStrategies.Count);
    }

    [Theory]
    [InlineData("snakeShot", "minWaitSeconds", "-1")]
    [InlineData("pullShot", "maxWaitSeconds", "0")]
    [InlineData("tickTackPass", "maxWaitSeconds", "1e20")]
    [InlineData("snakeShot", "waitDistribution", "\"gaussian\"")]
    [InlineData("pullShot", "variantSelection", "\"noRepeats\"")]
    [InlineData("snakeShot", "variants", "[]")]
    [InlineData("pullShot", "variants", "[{\"id\":\"push\",\"text\":\"Push\"}]")]
    [InlineData("tickTackPass", "variants", "[{\"id\":\"wall\",\"text\":\" \"}]")]
    [InlineData("snakeShot", "variants", "[{\"id\":\"push\",\"text\":\"A\"},{\"id\":\"push\",\"text\":\"B\"}]")]
    public void Invalid_strategy_config_is_rejected_even_when_disabled_and_initializer_preserves_file(string id, string property, string value)
    {
        JsonObject json = DefaultJson();
        json["training"]!["enabledStrategies"] = new JsonArray();
        json["training"]![id]![property] = JsonNode.Parse(value);
        AssertInvalidAndPreserved(json.ToJsonString());
    }

    [Theory]
    [InlineData("enabledStrategies", "[\"unknown\"]")]
    [InlineData("enabledStrategies", "[\"snakeShot\",\"snakeShot\"]")]
    [InlineData("enabledStrategies", "null")]
    [InlineData("snakeShot", "null")]
    public void Invalid_training_config_is_rejected(string property, string value)
    {
        JsonObject json = DefaultJson();
        json["training"]![property] = JsonNode.Parse(value);
        AssertInvalidAndPreserved(json.ToJsonString());
    }

    [Theory]
    [InlineData("snakeShot")]
    [InlineData("pullShot")]
    [InlineData("tickTackPass")]
    public void Recognition_is_an_implementation_detail_and_is_rejected_in_json(string id)
    {
        JsonObject json = DefaultJson();
        json["training"]![id]!["recognition"] = new JsonObject();
        Assert.Throws<JsonException>(() => RecorderSettingsJson.DeserializeAndValidate(json.ToJsonString()));
    }

    [Fact]
    public void Speech_is_not_a_training_setting()
    {
        JsonObject json = DefaultJson();
        json["training"]!["speech"] = new JsonObject();
        Assert.Throws<JsonException>(() => RecorderSettingsJson.DeserializeAndValidate(json.ToJsonString()));
    }

    [Fact]
    public void Serialized_config_places_training_last_and_contains_only_supported_fields()
    {
        JsonObject json = DefaultJson();
        Assert.Equal("training", json.Last().Key);
        JsonObject training = json["training"]!.AsObject();
        Assert.Equal(["enabledStrategies", "snakeShot", "pullShot", "tickTackPass"], training.Select(property => property.Key));
        foreach (string id in new[] { "snakeShot", "pullShot", "tickTackPass" })
        {
            Assert.Equal(["minWaitSeconds", "maxWaitSeconds", "waitDistribution", "variantSelection", "variants"],
                training[id]!.AsObject().Select(property => property.Key));
        }
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void Nonfinite_values_are_rejected_for_every_strategy(double value)
    {
        RecorderTrainingSettings settings = new();
        foreach (TrainingStrategySettings strategy in new TrainingStrategySettings[] { settings.SnakeShot, settings.PullShot, settings.TickTackPass })
        {
            strategy.MinWaitSeconds = value;
            Assert.Throws<InvalidOperationException>(() => settings.Validate());
            strategy.MinWaitSeconds = 1;
            strategy.MaxWaitSeconds = value;
            Assert.Throws<InvalidOperationException>(() => settings.Validate());
            strategy.MaxWaitSeconds = 10;
        }
    }

    [Fact]
    public void Empty_activation_is_valid_configuration_for_the_provider_fallback()
    {
        JsonObject json = DefaultJson();
        json["training"]!["enabledStrategies"] = new JsonArray();
        Assert.Empty(RecorderSettingsJson.DeserializeAndValidate(json.ToJsonString()).Training.EnabledStrategies);
    }

    private static JsonObject DefaultJson() => JsonNode.Parse(RecorderSettingsJson.Serialize(RecorderSettings.CreateDefault()))!.AsObject();

    private static TrainingStrategySettings Strategy(RecorderTrainingSettings settings, string id) => id switch
    {
        "snakeShot" => settings.SnakeShot,
        "pullShot" => settings.PullShot,
        _ => settings.TickTackPass,
    };

    private static void AssertInvalidAndPreserved(string text)
    {
        Assert.Throws<InvalidOperationException>(() => RecorderSettingsJson.DeserializeAndValidate(text));
        FakeSettingsFileStore files = new();
        string path = Path.Combine("external", "Config.json");
        files.Files[path] = text;
        RecorderSettingsContext context = new RecorderSettingsInitializer(files).Initialize("external", "fallback");
        Assert.Equal(SettingsConfigSource.DefaultsInvalidConfig, context.ConfigSource);
        Assert.NotEmpty(context.ConfigError!);
        Assert.Equal(text, files.Files[path]);
        Assert.Equal(3, context.Settings.Training.EnabledStrategies.Count);
    }
}
