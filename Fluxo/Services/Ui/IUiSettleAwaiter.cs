using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace Fluxo.Services.Ui;

public interface IUiSettleAwaiter
{
    Task WaitForUiReadyAsync(Window? owner = null, CancellationToken cancellationToken = default);
}
