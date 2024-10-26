// Copyright 2023-2024 Ville Penttinen
// Distributed under the MIT License.
// https://github.com/vipentti/dotnet-check-updates/blob/main/LICENSE.md

using DotnetCheckUpdates.Core;
using DotnetCheckUpdates.Core.Extensions;
using DotnetCheckUpdates.Core.ProjectModel;
using NuGet.Versioning;
using Spectre.Console;

namespace DotnetCheckUpdates.Commands.CheckUpdate;

internal partial class CheckUpdateCommand
{
    private abstract class InteractiveTreeItem
    {
        public abstract string Text { get; }

        public override string ToString() => ToStringImpl();

        protected abstract string ToStringImpl();
    }

    private sealed class InteractiveTreeSolution(string text) : InteractiveTreeItem
    {
        public override string Text => text;

        protected override string ToStringImpl() => $"Solution: {Text}";
    }

    private sealed class InteractiveTreeProject(string text) : InteractiveTreeItem
    {
        public override string Text => text;

        protected override string ToStringImpl() => $"Project: {Text}";
    }

    private sealed class InteractiveTreePackage(
        string projectName,
        string packageName,
        VersionRange newVersion,
        string text
    ) : InteractiveTreeItem
    {
        public string ProjectName { get; } = projectName;
        public string PackageName { get; } = packageName;
        public VersionRange NewVersion { get; } = newVersion;
        public override string Text { get; } = text;

        protected override string ToStringImpl() => $"{ProjectName}: {PackageName}: {NewVersion}";
    }

    private async Task ExecuteInteractiveUpgrade(
        string cwd,
        Settings settings,
        CancellationToken cancellationToken
    )
    {
        string FormatPath(string path) =>
            !settings.ShowAbsolute
                ? path.Replace(cwd ?? "", "")
                    .TrimStart(Path.DirectorySeparatorChar)
                    .TrimStart(Path.AltDirectorySeparatorChar)
                : path;

        var (projects, solutionProjectMap) = await DiscoverProjectsAndSolutions(cwd, settings);

        var longestPackageNameLength = 0;
        var longestVersionLength = 0;
        var totalPackageCount = 0;

        foreach (var project in projects)
        {
            LogProjectFound(_logger, project.FilePath, project.PackageCount);

            foreach (var pkg in project.PackageReferences)
            {
                longestPackageNameLength = Math.Max(longestPackageNameLength, pkg.Name.Length);
                longestVersionLength = Math.Max(
                    longestVersionLength,
                    pkg.GetVersionString().Length
                );
            }

            totalPackageCount += project.PackageCount;
        }

        if (totalPackageCount == 0)
        {
            _ansiConsole.MarkupLine("");
            _ansiConsole.WriteLine(CommonStrings.NoPackagesMatchFilters);
            return;
        }

        var projectsWithUpgrades = await GetProjectPackageUpgrades(
            settings,
            projects,
            cancellationToken
        );

        var prompt = new MultiSelectionPrompt<InteractiveTreeItem>
        {
            Title = CommonStrings.ChoosePackagesToUpdate,
            Converter = it => it.Text,
            PageSize = settings.InteractivePageSize ?? 10,
            InstructionsText =
                "[grey](Press <space> to select, <enter> to accept, <ctrl + c> to cancel)[/]",
        };
        var sb = new StringBuilder();
        var didHaveUpgrades = false;

        void WriteProjectsToSolutionOrPrompt(
            IEnumerable<(ProjectFile, PackageUpgradeVersionDictionary)> values,
            MultiSelectionPrompt<InteractiveTreeItem> prompt,
            IMultiSelectionItem<InteractiveTreeItem>? solutionGroup
        )
        {
            foreach (
                var (project, packages) in values.OrderBy(
                    it => it.Item1.FilePath,
                    StringComparer.Ordinal
                )
            )
            {
                var upgrades = CheckUpdateCommandHelpers.GetProjectPackageUpgrades(
                    project,
                    packages
                );

                if (upgrades.IsEmpty)
                {
                    continue;
                }

                didHaveUpgrades = true;

                var selections = upgrades.Select(FormatUpgrade).ToArray();

                var projectTree = new InteractiveTreeProject(FormatPath(project.FilePath));

                ISelectionItem<InteractiveTreeItem> projectGroup = solutionGroup is null
                    ? prompt.AddChoice(projectTree)
                    : solutionGroup.AddChild(projectTree);

                foreach (var item in selections)
                {
                    projectGroup.AddChild(item);
                }

                InteractiveTreePackage FormatUpgrade(PackageUpgrade it)
                {
                    sb.Clear();
                    var nameLengthToPad = longestPackageNameLength - it.Name.Length;
                    sb.Append(it.Name);
                    sb.Append(' ', nameLengthToPad + 1);

                    var originalVersionString = it.From.VersionString();

                    sb.Append(originalVersionString.EscapeMarkup());
                    sb.Append(' ', longestVersionLength - originalVersionString.Length + 1);
                    sb.Append(" → ");
                    sb.Append(CheckUpdateCommandHelpers.GetUpgradedVersionString(it.From, it.To));

                    return new(project.FilePath, it.Name, it.To, sb.ToString());
                }
            }
        }

        if (solutionProjectMap.Count > 0)
        {
            foreach (var (solution, solutionProjectArray) in solutionProjectMap)
            {
                var solutionGroup = prompt.AddChoice(
                    new InteractiveTreeSolution(FormatPath(solution))
                );

                WriteProjectsToSolutionOrPrompt(
                    projectsWithUpgrades.Where(it =>
                        solutionProjectArray.Includes(it.project.FilePath)
                    ),
                    prompt,
                    solutionGroup
                );
            }
        }
        else
        {
            WriteProjectsToSolutionOrPrompt(projectsWithUpgrades, prompt, null);
        }

        if (!didHaveUpgrades)
        {
            _ansiConsole.WriteLine();
            _ansiConsole.WriteLine(CommonStrings.AllPackagesMatchLatestVersion);
            return;
        }

        ImmutableArray<InteractiveTreePackage> choices;

        try
        {
            choices = (await prompt.ShowAsync(_ansiConsole, cancellationToken))
                .OfType<InteractiveTreePackage>()
                .ToImmutableArray();
        }
        catch (TaskCanceledException ex)
        {
            throw new PromptCanceledException("Prompt was canceled", ex);
        }

        var originalProjects = ImmutableArray.CreateBuilder<ProjectFile>();
        var newProjects = ImmutableArray.CreateBuilder<ProjectFile>();

        foreach (var (project, packages) in projectsWithUpgrades)
        {
            var allProjectChoices = choices
                .Where(it =>
                    string.Equals(it.ProjectName, project.FilePath, StringComparison.Ordinal)
                )
                .ToImmutableArray();

            var selectedUpgrades = new PackageUpgradeVersionDictionary(packages.Count);

            foreach (var choice in allProjectChoices)
            {
                if (packages.TryGetValue(choice.PackageName, out var version))
                {
                    selectedUpgrades[choice.PackageName] = version;
                }
            }

            originalProjects.Add(project);
            var newProject = project.UpdatePackageReferences(selectedUpgrades);
            newProjects.Add(newProject);
        }

        _ansiConsole.WriteLine(CommonStrings.UpgradingSelectedPackages);

        var upgradedProjects = new List<ProjectFile>();

        if (solutionProjectMap.Count > 0)
        {
            RenderSolutionUpgrades(
                new SolutionProjectRenderOptions()
                {
                    SolutionProjectMap = solutionProjectMap,
                    FormatPath = FormatPath,
                    OriginalProjects = originalProjects.ToImmutable(),
                    NewProjects = newProjects.ToImmutable(),
                    Settings = settings,
                    HideIfNoUpgrade = true,
                },
                out upgradedProjects
            );
        }
        else
        {
            var root = new Tree("Projects");

            if (settings.AsciiTree)
            {
                root.Guide = TreeGuide.Ascii;
            }

            CheckUpdateCommandHelpers.SetupGridInTree(
                FormatPath,
                root,
                projects,
                newProjects,
                upgradedProjects,
                settings,
                longestPackageNameLength,
                longestVersionLength,
                hideIfNoUpgrade: true
            );
            _ansiConsole.Write(root);
        }

        if (upgradedProjects.Count > 0)
        {
            _ansiConsole.MarkupLine("");
            foreach (var proj in upgradedProjects)
            {
                _ansiConsole.MarkupLine(
                    $"Upgrading packages in [yellow]{FormatPath(proj.FilePath)}[/]"
                );
                proj.Save(_fileSystem);
            }

            _ansiConsole.MarkupLine("");

            if (settings.Restore)
            {
                await RestorePackages(
                    solutionProjectMap,
                    solutionProjectMap.Count > 0,
                    upgradedProjects,
                    FormatPath
                );
            }
            else
            {
                _ansiConsole.MarkupLine("Run [blue]dotnet restore[/] to install new versions");
            }
            _ansiConsole.MarkupLine("");
        }
    }
}
