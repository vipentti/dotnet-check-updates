// Copyright 2023-2024 Ville Penttinen
// Distributed under the MIT License.
// https://github.com/vipentti/dotnet-check-updates/blob/main/LICENSE.md

using System.Collections.Generic;
using Nuke.Common.CI.GitHubActions;
using Nuke.Common.ProjectModel;
using Nuke.Components;
using Vipentti.Nuke.Components;
using static Vipentti.Nuke.Components.StandardNames;

namespace build;

[ExtendedGitHubActions(
    "pull-request",
    GitHubActionsImage.WindowsLatest,
    GitHubActionsImage.UbuntuLatest,
    GitHubActionsImage.MacOsLatest,
    OnPullRequestBranches = [MainBranch, DevelopBranch],
    PublishArtifacts = false,
    FetchDepth = 0 // fetch full history
    , SetupDotnetVersions =
    [
        "6.x",
        "8.x",
    ]
    , InvokedTargets =
    [
        nameof(IUseLocalDotNetTools.RestoreLocalTools),
        nameof(IUseLinters.InstallLinters),
        nameof(ITest.Test),
        nameof(IUseLinters.Lint),
        nameof(IValidatePackages.ValidatePackages),
    ])]
[StandardPublishGitHubActions(
    "publish",
    GitHubActionsImage.WindowsLatest,
    GitHubActionsImage.UbuntuLatest,
    GitHubActionsImage.MacOsLatest
    , OnPushBranches = [MainBranch]
    , SetupDotnetVersions =
    [
        "6.x",
        "8.x",
    ]
    , InvokedTargets =
    [
        nameof(IUseLocalDotNetTools.RestoreLocalTools),
    ]
)]
partial class Build : StandardNukeBuild, IUseCsharpier
{
    public override string OriginalRepositoryName { get; } = "dotnet-check-updates";
    public override string MainReleaseBranch { get; } = MainBranch;
    public override IEnumerable<Project> ProjectsToPack =>
    [
        CurrentSolution.GetSolutionFolder("src").GetProject("DotnetCheckUpdates"),
    ];

    public override IEnumerable<IProvideLinter> Linters =>
    [
        From<IUseDotNetFormat>().Linter,
        From<IUseCsharpier>().Linter,
    ];

    public override IEnumerable<IProvideFormatter> Formatters =>
    [
        From<IUseDotNetFormat>().Formatter,
        From<IUseCsharpier>().Formatter,
    ];

    bool IUseCsharpier.UseGlobalTool { get; } = false;

    public override IEnumerable<Project> TestProjects => CurrentSolution.GetAllProjects("*Tests*");

    public override bool SignReleaseTags { get; } = true;

    // Support plugins are available for:
    //   - JetBrains ReSharper        https://nuke.build/resharper
    //   - JetBrains Rider            https://nuke.build/rider
    //   - Microsoft VisualStudio     https://nuke.build/visualstudio
    //   - Microsoft VSCode           https://nuke.build/vscode

    public static int Main() => Execute<Build>(x => x.From<ICompile>().Compile);
}
