// Copyright 2023 Ville Penttinen
// Distributed under the MIT License.
// https://github.com/vipentti/dotnet-check-updates/blob/main/LICENSE.md

using Nuke.Common;
using Nuke.Common.Tools.DotNet;
using Nuke.Components;
using Vipentti.Nuke.Components;
using static Nuke.Common.Tools.DotNet.DotNetTasks;

namespace build;

partial class Build
{
#pragma warning disable IDE0051 // Remove unused private members
    Target UpdateLocalTool => _ => _
        .DependsOn<IPack>(x => x.Pack)
        .DependsOn<IUseLinters>(x => x.Lint)
        .After<IPack>(x => x.Pack)
        .Executes(() =>
        {
            DotNetToolUpdate(_ => _
                .SetGlobal(true)
                .AddSources(From<IPack>().PackagesDirectory)
                .SetPackageName("dotnet-check-updates")
            );
        });
#pragma warning restore IDE0051 // Remove unused private members
}
