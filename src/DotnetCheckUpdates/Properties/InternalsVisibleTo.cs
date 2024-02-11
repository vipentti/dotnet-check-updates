// Copyright 2023-2024 Ville Penttinen
// Distributed under the MIT License.
// https://github.com/vipentti/dotnet-check-updates/blob/main/LICENSE.md

using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("DotnetCheckUpdates.Tests")]
// Allow NSubstitute to generate stubs
[assembly: InternalsVisibleTo("DynamicProxyGenAssembly2")]
