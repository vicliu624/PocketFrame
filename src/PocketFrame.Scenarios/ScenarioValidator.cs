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

    private static string Normalize(string value) => value.Trim().Replace("_", string.Empty, StringComparison.Ordinal).ToLowerInvariant();
}

public sealed record ScenarioValidationResult(bool IsValid, IReadOnlyList<string> Errors);
