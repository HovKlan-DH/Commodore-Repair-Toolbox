using System.Threading;
using Avalonia.Headless;

namespace ClassicRepairToolbox.Tests.Ui;

// ###########################################################################################
// Runs a test body on Avalonia's UI thread inside a headless session.
//
// Avalonia ships an xunit adapter ([AvaloniaFact]), but the 12.1.1 build of it depends on
// xunit v3 while this suite is on xunit 2.9.3 - referencing it makes every Fact and
// InlineData in the project ambiguous. The session API underneath the adapter is public,
// so the tests use it directly and stay on xunit 2.
//
// Everything touching a control must go through Run: Avalonia requires a dispatcher and
// will throw if a visual is created on an arbitrary thread.
// ###########################################################################################
public static class UiTest
{
    // One session per assembly. Starting it is expensive (it spins up the app, its styles
    // and its resource dictionaries), so it is created once and reused by every UI test.
    private static readonly HeadlessUnitTestSession Session =
        HeadlessUnitTestSession.GetOrStartForAssembly(typeof(UiTest).Assembly);

    public static void Run(Action body)
    {
        Session.Dispatch(body, CancellationToken.None).GetAwaiter().GetResult();
    }
}

// UI tests share one dispatcher thread, so they are kept in a single xunit collection
// rather than being run in parallel against each other.
[CollectionDefinition("HeadlessUi")]
public class HeadlessUiCollection
{
}
