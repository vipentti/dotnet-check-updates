// Copyright 2023-2026 Ville Penttinen
// Distributed under the MIT License.
// https://github.com/vipentti/dotnet-check-updates/blob/main/LICENSE.md

namespace DotnetCheckUpdates.Core.ProjectModel;

internal enum TargetFrameworkConditionOperator
{
    Equals,
    NotEquals,
}

internal sealed record TargetFrameworkCondition(
    TargetFrameworkConditionOperator Operator,
    string TargetFramework,
    string RawCondition,
    int GroupId = 0
)
{
    public bool IsMatch(string targetFramework) =>
        Operator switch
        {
            TargetFrameworkConditionOperator.Equals => string.Equals(
                TargetFramework,
                targetFramework,
                StringComparison.OrdinalIgnoreCase
            ),
            TargetFrameworkConditionOperator.NotEquals => !string.Equals(
                TargetFramework,
                targetFramework,
                StringComparison.OrdinalIgnoreCase
            ),
            _ => false,
        };

    public string ToDisplayString()
    {
        return Operator == TargetFrameworkConditionOperator.Equals
            ? TargetFramework
            : $"!= {TargetFramework}";
    }
}
