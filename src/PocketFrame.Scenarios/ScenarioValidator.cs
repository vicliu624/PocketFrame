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

        return new ScenarioValidationResult(errors.Count == 0, errors);
    }
}

public sealed record ScenarioValidationResult(bool IsValid, IReadOnlyList<string> Errors);
