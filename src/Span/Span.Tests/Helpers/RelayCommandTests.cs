using Span.Helpers;

namespace Span.Tests.Helpers;

[TestClass]
public class RelayCommandTests
{
    [TestMethod]
    public void ValueTypeCommandRejectsNullAndWrongType()
    {
        int calls = 0;
        var command = new RelayCommand<int>(_ => calls++);
        Assert.IsFalse(command.CanExecute(null));
        Assert.IsFalse(command.CanExecute("wrong type"));
        command.Execute(null);
        command.Execute("wrong type");
        Assert.AreEqual(0, calls);
    }

    [TestMethod]
    public void ReferenceTypeCommandPreservesNullParameter()
    {
        bool called = false;
        string? received = "initial";
        var command = new RelayCommand<string?>(value => { called = true; received = value; });
        Assert.IsTrue(command.CanExecute(null));
        command.Execute(null);
        Assert.IsTrue(called);
        Assert.IsNull(received);
    }

    [TestMethod]
    public void TypedParameterReachesPredicateAndAction()
    {
        int received = 0;
        var command = new RelayCommand<int>(value => received = value, value => value > 0);
        Assert.IsFalse(command.CanExecute(-1));
        Assert.IsTrue(command.CanExecute(7));
        command.Execute(7);
        Assert.AreEqual(7, received);
    }

    [TestMethod]
    public void NullableValueTypeCommandAcceptsNull()
    {
        int? received = 1;
        var command = new RelayCommand<int?>(value => received = value, value => value == null);
        Assert.IsTrue(command.CanExecute(null));
        command.Execute(null);
        Assert.IsNull(received);
    }

    [TestMethod]
    public void CanExecuteChangedIsRaisedOnRequest()
    {
        int notifications = 0;
        var command = new RelayCommand<int>(_ => { });
        EventHandler handler = (_, _) => notifications++;
        command.CanExecuteChanged += handler;
        command.RaiseCanExecuteChanged();
        command.CanExecuteChanged -= handler;
        command.RaiseCanExecuteChanged();
        Assert.AreEqual(1, notifications);
    }
}