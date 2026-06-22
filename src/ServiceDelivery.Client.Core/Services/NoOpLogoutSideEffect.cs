using System.Threading.Tasks;
using ServiceDelivery.Client.Core.Interfaces;

namespace ServiceDelivery.Client.Core.Services;

/// <summary>
/// Null-object default for <see cref="ILogoutSideEffect"/>. Completes immediately and honours the
/// contract without throwing (Liskov). FE-023 registers the real heartbeat-stop implementation,
/// replacing this registration at the composition root with no change to the shell.
/// </summary>
public class NoOpLogoutSideEffect : ILogoutSideEffect
{
    public Task RunBeforeTokenClearedAsync() => Task.CompletedTask;
}
