// Copyright 2023 Ville Penttinen
// Distributed under the MIT License.
// https://github.com/vipentti/dotnet-check-updates/blob/main/LICENSE.md

namespace DotnetCheckUpdates.Core.Utils;

internal sealed class ApplicationExitHandler : IDisposable
{
    private readonly CancellationTokenSource _forcefulExit;
    private readonly CancellationTokenSource _gracefulExit;
    private bool _didExit;

    public ApplicationExitHandler()
    {
        _forcefulExit = new();
        _gracefulExit = new();
    }

    public CancellationToken ForcefulToken => _forcefulExit.Token;
    public CancellationToken GracefulToken => _gracefulExit.Token;

    public void Exit(bool force)
    {
        if (_didExit)
        {
            return;
        }

        _didExit = true;

        if (force)
        {
            TryCancel(_gracefulExit);
            TryCancel(_forcefulExit);
        }
        else
        {
            TryCancel(_gracefulExit);
            TryCancelAfter(_forcefulExit, TimeSpan.FromSeconds(2));
        }

        static void TryCancel(CancellationTokenSource source)
        {
            try
            {
                source.Cancel();
            }
            catch (ObjectDisposedException)
            {
                // ignored
            }
        }

        static void TryCancelAfter(CancellationTokenSource source, TimeSpan delay)
        {
            try
            {
                source.CancelAfter(delay);
            }
            catch (ObjectDisposedException)
            {
                // ignored
            }
        }
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);

        _gracefulExit.Dispose();
        _forcefulExit.Dispose();
    }
}
