using CRT;

namespace ClassicRepairToolbox.Tests.Ui;

// ###########################################################################################
// Every tab is constructed for real, headlessly, with the app's actual App.axaml styles and
// resource dictionaries loaded.
//
// This is the only automated coverage of Tabs/ - the unit tests underneath cover Handlers/
// and never touch a control. Constructing a tab runs InitializeComponent (so the .axaml must
// parse, and every x:Name the code-behind reaches for must still exist) and then the
// constructor body, which for most tabs wires event handlers to those named controls. Rename
// or delete a control in the .axaml without updating the code-behind and these fail, where
// previously nothing would notice until the tab was opened by hand.
//
// "Does not throw" is the whole assertion, deliberately. Whether the layout LOOKS right still
// needs a human running the app; whether it can be built at all no longer does.
// ###########################################################################################
[Collection("HeadlessUi")]
public class TabConstructionTests
{
    [Fact]
    public void The_about_tab_can_be_constructed()
    {
        UiTest.Run(() => Assert.NotNull(new TabAbout()));
    }

    [Fact]
    public void The_configuration_tab_can_be_constructed()
    {
        // Reads UserSettings statics in its constructor. No seeding here on purpose: the
        // defaults are enough to prove it builds, and reading them cannot throw.
        UiTest.Run(() => Assert.NotNull(new TabConfiguration()));
    }

    [Fact]
    public void The_contribute_tab_can_be_constructed()
    {
        UiTest.Run(() => Assert.NotNull(new TabContribute()));
    }

    [Fact]
    public void The_feedback_tab_can_be_constructed()
    {
        UiTest.Run(() => Assert.NotNull(new TabFeedback()));
    }

    [Fact]
    public void The_oscilloscope_tab_can_be_constructed()
    {
        UiTest.Run(() => Assert.NotNull(new TabOscilloscope()));
    }

    [Fact]
    public void The_overview_tab_can_be_constructed()
    {
        UiTest.Run(() => Assert.NotNull(new TabOverview()));
    }

    [Fact]
    public void The_resources_tab_can_be_constructed()
    {
        UiTest.Run(() => Assert.NotNull(new TabResources()));
    }

    [Fact]
    public void The_schematics_tab_can_be_constructed()
    {
        // The heaviest one: its constructor wires seventeen handlers to named controls, so it
        // is the tab most likely to break silently when the .axaml is edited.
        UiTest.Run(() => Assert.NotNull(new TabSchematics()));
    }
}
