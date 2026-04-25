namespace PocketFrame.Scenarios;

public sealed class ScenarioValidator
{
    public ScenarioValidationResult Validate(ScenarioDefinition scenario)
    {
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(scenario.Name))
        {
            errors.Add("Scenario name is required.");
        }

        if (string.IsNullOrWhiteSpace(scenario.DeviceId))
        {
            errors.Add("Scenario deviceId is required.");
        }

        if (string.IsNullOrWhiteSpace(scenario.Connection.Host))
        {
            errors.Add("Scenario connection.host is required.");
        }

        if (scenario.Connection.Port <= 0 || scenario.Connection.Port > 65535)
        {
            errors.Add("Scenario connection.port must be between 1 and 65535.");
        }

        if (scenario.Scale <= 0)
        {
            errors.Add("Scenario scale must be greater than 0.");
        }

        ValidateEnvironment(scenario.Environment, errors);
        if (string.IsNullOrWhiteSpace(scenario.Captures.OutputDir))
        {
            errors.Add("Scenario captures.outputDir is required.");
        }

        if (string.IsNullOrWhiteSpace(scenario.Run.WorkingDir))
        {
            errors.Add("Scenario run.workingDir is required.");
        }

        ValidateActions(scenario.Actions, errors);
        ValidateAssertions(scenario.Assertions, errors);

        return new ScenarioValidationResult(errors.Count == 0, errors);
    }

    private static void ValidateEnvironment(ScenarioEnvironment environment, List<string> errors)
    {
        if (string.IsNullOrWhiteSpace(environment.ProfileId) &&
            (!string.IsNullOrWhiteSpace(environment.Type) ||
             !string.IsNullOrWhiteSpace(environment.Distro) ||
             !string.IsNullOrWhiteSpace(environment.WorkingDirectory) ||
             environment.PreCommands.Count > 0 ||
             environment.PostCommands.Count > 0))
        {
            errors.Add("Scenario environment.profileId is required when environment settings are provided.");
        }

        if (!string.IsNullOrWhiteSpace(environment.Type) &&
            !environment.Type.Equals("wsl", StringComparison.OrdinalIgnoreCase) &&
            !environment.Type.Equals("local", StringComparison.OrdinalIgnoreCase))
        {
            errors.Add($"Scenario environment.type is unsupported: {environment.Type}");
        }

        if (!string.IsNullOrWhiteSpace(environment.VncGeometry) && !LooksLikeGeometry(environment.VncGeometry))
        {
            errors.Add("Scenario environment.vncGeometry must look like 320x170.");
        }

        if (environment.VncDepth is not 0 and not (8 or 16 or 24 or 32))
        {
            errors.Add("Scenario environment.vncDepth must be 8, 16, 24, or 32.");
        }

        ValidateEnvironmentCommands(environment.PreCommands, "environment.preCommands", errors);
        ValidateEnvironmentCommands(environment.PostCommands, "environment.postCommands", errors);
    }

    private static void ValidateEnvironmentCommands(IReadOnlyList<ScenarioEnvironmentCommand> commands, string name, List<string> errors)
    {
        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (var index = 0; index < commands.Count; index++)
        {
            var command = commands[index];
            if (!string.IsNullOrWhiteSpace(command.Id) && !ids.Add(command.Id))
            {
                errors.Add($"Scenario {name} id must be unique: {command.Id}");
            }

            if (string.IsNullOrWhiteSpace(command.Command))
            {
                errors.Add($"Scenario {name}[{index}].command is required.");
            }

            if (command.TimeoutMs < 0)
            {
                errors.Add($"Scenario {name}[{index}].timeoutMs must be non-negative.");
            }
        }
    }

    private static void ValidateActions(IReadOnlyList<ScenarioAction> actions, List<string> errors)
    {
        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (var index = 0; index < actions.Count; index++)
        {
            var action = actions[index];
            var actionName = string.IsNullOrWhiteSpace(action.Id) ? $"action[{index}]" : action.Id;
            if (!string.IsNullOrWhiteSpace(action.Id) && !ids.Add(action.Id))
            {
                errors.Add($"Scenario action id must be unique: {action.Id}");
            }

            switch (Normalize(action.Type))
            {
                case "wait":
                case "delay":
                    if (action.DurationMs <= 0)
                    {
                        errors.Add($"{actionName}.durationMs must be greater than 0.");
                    }

                    break;
                case "waitframechange":
                    if (action.TimeoutMs <= 0)
                    {
                        errors.Add($"{actionName}.timeoutMs must be greater than 0.");
                    }

                    if (action.AfterFrame < 0)
                    {
                        errors.Add($"{actionName}.afterFrame must be non-negative.");
                    }

                    break;
                case "waitstableframe":
                    if (action.QuietMs <= 0)
                    {
                        errors.Add($"{actionName}.quietMs must be greater than 0.");
                    }

                    if (action.TimeoutMs <= 0)
                    {
                        errors.Add($"{actionName}.timeoutMs must be greater than 0.");
                    }

                    break;
                case "capturescreen":
                case "capturedevice":
                case "savetrace":
                    break;
                case "presskey":
                    Require(action.Key, $"{actionName}.key is required.", errors);
                    break;
                case "pressbutton":
                    Require(action.ButtonId, $"{actionName}.buttonId is required.", errors);
                    if (action.DurationMs < 0)
                    {
                        errors.Add($"{actionName}.durationMs must be non-negative.");
                    }

                    break;
                case "clickscreen":
                    if (action.X < 0 || action.Y < 0)
                    {
                        errors.Add($"{actionName}.x and y must be non-negative.");
                    }

                    break;
                case "typetext":
                    Require(action.Text, $"{actionName}.text is required.", errors);
                    break;
                default:
                    errors.Add($"{actionName}.type is unsupported: {action.Type}");
                    break;
            }
        }
    }

    private static void ValidateAssertions(IReadOnlyList<ScenarioAssertion> assertions, List<string> errors)
    {
        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (var index = 0; index < assertions.Count; index++)
        {
            var assertion = assertions[index];
            var assertionName = string.IsNullOrWhiteSpace(assertion.Id) ? $"assertion[{index}]" : assertion.Id;
            if (!string.IsNullOrWhiteSpace(assertion.Id) && !ids.Add(assertion.Id))
            {
                errors.Add($"Scenario assertion id must be unique: {assertion.Id}");
            }

            switch (Normalize(assertion.Type))
            {
                case "framechanged":
                    Require(assertion.AfterAction, $"{assertionName}.afterAction is required.", errors);
                    break;
                case "screenshotexists":
                    Require(assertion.Label, $"{assertionName}.label is required.", errors);
                    break;
                case "screenshotmatchesbaseline":
                    Require(assertion.Label, $"{assertionName}.label is required.", errors);
                    Require(assertion.Baseline, $"{assertionName}.baseline is required.", errors);
                    if (assertion.Threshold < 0 || assertion.Threshold > 1)
                    {
                        errors.Add($"{assertionName}.threshold must be between 0 and 1.");
                    }

                    if (assertion.PixelTolerance < 0 || assertion.PixelTolerance > 255)
                    {
                        errors.Add($"{assertionName}.pixelTolerance must be between 0 and 255.");
                    }

                    ValidateRegions(assertion.Regions, $"{assertionName}.regions", errors);
                    ValidateRegions(assertion.IgnoreRegions, $"{assertionName}.ignoreRegions", errors);
                    ValidateRegions(assertion.MaskRegions, $"{assertionName}.maskRegions", errors);
                    break;
                case "framehashnotempty":
                    break;
                case "framehashequals":
                case "framehashnotequals":
                    Require(assertion.ExpectedHash, $"{assertionName}.expectedHash is required.", errors);
                    break;
                case "actionsucceeded":
                case "actionfailed":
                    Require(assertion.AfterAction, $"{assertionName}.afterAction is required.", errors);
                    break;
                case "allactionssucceeded":
                    break;
                default:
                    errors.Add($"{assertionName}.type is unsupported: {assertion.Type}");
                    break;
            }
        }
    }

    private static void Require(string value, string message, List<string> errors)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            errors.Add(message);
        }
    }

    private static void ValidateRegions(IReadOnlyList<ScenarioRegion> regions, string name, List<string> errors)
    {
        for (var index = 0; index < regions.Count; index++)
        {
            var region = regions[index];
            if (region.X < 0 || region.Y < 0)
            {
                errors.Add($"{name}[{index}].x and y must be non-negative.");
            }

            if (region.Width <= 0 || region.Height <= 0)
            {
                errors.Add($"{name}[{index}].width and height must be greater than 0.");
            }
        }
    }

    private static string Normalize(string value) => value.Trim().Replace("_", string.Empty, StringComparison.Ordinal).ToLowerInvariant();

    private static bool LooksLikeGeometry(string value)
    {
        var parts = value.Split('x', 'X');
        return parts.Length == 2 &&
               int.TryParse(parts[0], out var width) &&
               int.TryParse(parts[1], out var height) &&
               width > 0 &&
               height > 0;
    }
}

public sealed record ScenarioValidationResult(bool IsValid, IReadOnlyList<string> Errors);
