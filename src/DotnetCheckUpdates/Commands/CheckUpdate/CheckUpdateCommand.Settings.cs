// Copyright 2023-2024 Ville Penttinen
// Distributed under the MIT License.
// https://github.com/vipentti/dotnet-check-updates/blob/main/LICENSE.md

using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using DotnetCheckUpdates.Core;
using Spectre.Console.Cli;
using ValidationResult = Spectre.Console.ValidationResult;

namespace DotnetCheckUpdates.Commands.CheckUpdate;

internal partial class CheckUpdateCommand
{
    internal class Settings : CommandSettings
    {
        [CommandOption("--version")]
        [Description("Shows the version of the application. Exits after outputting the version. ")]
        public bool ShowVersion { get; init; }

        [CommandOption("--show-absolute")]
        [Description("Shows absolute paths when printing projects and solutions. (default: false)")]
        public bool ShowAbsolute { get; init; }

        [CommandOption("--show-package-count")]
        [Description(
            "Shows number of packages found in each project after filtering has been applied"
        )]
        public bool ShowPackageCount { get; init; }

        [CommandOption("--cwd <CurrentDirectory>")]
        [Description("Sets the current working directory. (default: current directory)")]
        public string? Cwd { get; init; }

        [CommandOption("-s|--solution <Solution>")]
        [Description(
            "Path to a .sln file to search for projects. Exclusive with --project. (default: searches for .sln files in the current directory if no .csproj files are found)"
        )]
        public string? Solution { get; init; }

        [CommandOption("-p|--project <Project>")]
        [Description(
            "Path to a .csproj file to search for upgrades. Exclusive with --solution. (default: searches for .csproj files in the current directory)"
        )]
        public string? Project { get; init; }

        [CommandOption("-r|--recurse")]
        [Description("Recursively searches for projects. (default: false)")]
        public bool Recurse { get; init; }

        [CommandOption("-d|--depth <Depth>")]
        [Range(0, 16)]
        [Description(
            """
                Recursion depth. Only applied when --recurse is also specified.
                Minimum is 1 and maximum is 16.
                Set to 0 to search arbitrary directory depth. (default: 4)
                """
        )]
        public int Depth { get; init; } = 4;

        [CommandOption("--restore")]
        [Description("Restores packages after upgrade. (default: false)")]
        public bool Restore { get; init; }

        [CommandOption("-l|--list")]
        [Description(
            "Lists current packages and their versions even if they have no upgrade available. (default: false)"
        )]
        public bool List { get; init; }

        [CommandOption("-u|--upgrade")]
        [Description(
            "Upgrades packages to the appropriate version based on the current --target option. (default: false)"
        )]
        public bool Upgrade { get; init; }

        [CommandOption("--concurrency <Number>")]
        [Range(1, 32)]
        [Description(
            "Max number of concurrent HTTP requests. Minimum is 1 and maximum is 32. (default: 8)"
        )]
        public int Concurrency { get; init; } = 8;

        [CommandOption("-i|--interactive")]
        [Description("Enable interactive prompts for each package. Implies --upgrade.")]
        public bool Interactive { get; init; }

        [CommandOption("-f|-I|--filter|--include|--inc")]
        [Description(
            """
                Include only package names matching the given string, glob or comma-or-space-delimited list.
                Example: System*
                Aliases: -I, --include, --inc
                """
        )]
        public string[] Include { get; init; } = [];

        [CommandOption("-x|-E|--exclude|--reject|--exc")]
        [Description(
            """
                Exclude package names matching the given string, glob or comma-or-space-delimited list.
                Example: System*
                Aliases: -E, --reject, --exc
                """
        )]
        public string[] Exclude { get; init; } = [];

        [CommandOption("-t|--target <Target>")]
        [Description(
            """
                Determines the version to upgrade to:
                latest, greatest, major, minor, patch, pre-major, pre-minor, pre-patch. (default: latest)
                """
        )]
        [TypeConverter(typeof(UpgradeTargetConverter))]
        public UpgradeTarget Target { get; init; } = UpgradeTarget.Latest;

        public bool AsciiTree { get; init; }

        /// <summary>
        /// Hide the progress bar (used by interactive tests)
        /// from the console
        /// </summary>
        internal bool HideProgressAfterComplete { get; init; }

        public override ValidationResult Validate()
        {
            var context = new ValidationContext(this);

            var results = new List<System.ComponentModel.DataAnnotations.ValidationResult>();

            var isValid = Validator.TryValidateObject(
                this,
                context,
                results,
                validateAllProperties: true
            );
            var errors = new List<string>();

            if (!string.IsNullOrWhiteSpace(Project) && !string.IsNullOrWhiteSpace(Solution))
            {
                errors.Add("Only one of --project, --solution may be specified.");
            }

            if (!isValid)
            {
                var props = typeof(Settings).GetProperties();

                var resultsByName = results.ToDictionary(
                    it => string.Join(".", it.MemberNames),
                    it => it.ErrorMessage ?? ""
                );

                foreach (var prop in props)
                {
                    if (resultsByName.TryGetValue(prop.Name, out var error))
                    {
                        var commandOption = prop.GetCustomAttributes(true)
                            .OfType<CommandOptionAttribute>()
                            .FirstOrDefault();

                        if (commandOption is not null)
                        {
                            string? name = null;

                            if (
                                commandOption.LongNames.Count > 0
                                && commandOption.LongNames[0] is string longName
                            )
                            {
                                name = "--" + longName;
                            }
                            else if (
                                commandOption.ShortNames.Count > 0
                                && commandOption.ShortNames[0] is string shortName
                            )
                            {
                                name = "-" + shortName;
                            }

                            if (name is not null)
                            {
                                error = error.Replace($"The field {prop.Name}", name);
                            }
                        }

                        errors.Add(error);
                    }
                }
            }

            if (errors.Count > 0)
            {
                return ValidationResult.Error(string.Join(" ", errors));
            }

            return ValidationResult.Success();
        }

        public string GetUpgradeCommandHelpText()
        {
            var sb = new System.Text.StringBuilder();

            sb.AppendLine();
            sb.Append("Run [cyan]").Append(CliConstants.CliName);

            if (!string.IsNullOrEmpty(Cwd))
            {
                sb.Append(" --cwd ").Append(Cwd);
            }

            if (!string.IsNullOrEmpty(Project))
            {
                sb.Append(" --project ").Append(Project);
            }

            if (!string.IsNullOrEmpty(Solution))
            {
                sb.Append(" --solution ").Append(Solution);
            }

            if (Recurse)
            {
                sb.Append(" -r");
            }

            if (Recurse && Depth != 4)
            {
                sb.Append(" -d ").Append(Depth);
            }

            sb.Append(" -u[/] to upgrade");

            return sb.ToString();
        }
    }
}
