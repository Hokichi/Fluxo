using CommunityToolkit.Mvvm.Messaging;
using Fluxo.Core.Interfaces;
using Fluxo.ViewModels.Shell.QuickSetupWizard;
using NSubstitute;
using Xunit;

namespace Fluxo.Tests.ViewModels.Shell.QuickSetupWizard;

public sealed class QuickSetupWizardDraftCollectionTests
{
    [Fact]
    public void NextTempId_DecrementsForEachNewSource()
    {
        var unitOfWork = Substitute.For<IUnitOfWork>();
        var vm = new QuickSetupWizardSpendingSourcesVM(null!, unitOfWork, new WeakReferenceMessenger());

        var first = vm.GetNextTemporaryId();
        var second = vm.GetNextTemporaryId();

        Assert.Equal(-1, first);
        Assert.Equal(-2, second);
    }
}
