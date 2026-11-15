// Author: rstewa · https://github.com/rstewa
// Created: 09/29/2024
// Updated: 10/17/2024

using System;
using System.Threading;
using System.Threading.Tasks;

namespace Audibly.App.Services;

public static class DialogManager
{
    private static readonly SemaphoreSlim _oneAtATimeAsync = new(1, 1);

    internal static async Task<T> OneAtATimeAsync<T>(Func<Task<T>> show, TimeSpan? timeout, CancellationToken? token)
    {
        var to = timeout ?? TimeSpan.FromHours(1);
        var tk = token ?? new CancellationToken(false);
        if (!await _oneAtATimeAsync.WaitAsync(to, tk))
            throw new Exception($"{nameof(DialogManager)}.{nameof(OneAtATimeAsync)} has timed out.");
        try
        {
            return await show();
        }
        finally
        {
            _oneAtATimeAsync.Release();
        }
    }

    /// <summary>
    ///     Like <see cref="OneAtATimeAsync{T}" />, but gives up quietly when another dialog keeps the
    ///     queue busy for longer than <paramref name="wait" />. For questions that can be asked again
    ///     later and must not hang unseen behind another dialog.
    /// </summary>
    internal static async Task<(bool Shown, T? Result)> TryOneAtATimeAsync<T>(Func<Task<T>> show, TimeSpan wait)
    {
        if (!await _oneAtATimeAsync.WaitAsync(wait)) return (false, default);
        try
        {
            return (true, await show());
        }
        finally
        {
            _oneAtATimeAsync.Release();
        }
    }
}