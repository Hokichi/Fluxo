using CommunityToolkit.Mvvm.Messaging;
using Fluxo.Core.Interfaces;
using Fluxo.ViewModels.Shell.StartupWizard;
using NSubstitute;
using Xunit;

namespace Fluxo.Tests.ViewModels.Shell.StartupWizard;

public sealed class StartupWizardDraftCollectionTests
{
    [Fact]
    public void NextTempId_DecrementsForEachNewSource()
    {
        var unitOfWork = Substitute.For<IUnitOfWork>();
        var vm = new StartupWizardSpendingSourcesVM(null!, unitOfWork, new WeakReferenceMessenger());

        var first = vm.GetNextTemporaryId();
        var second = vm.GetNextTemporaryId();

        Assert.Equal(-1, first);
        Assert.Equal(-2, second);
    }
}
