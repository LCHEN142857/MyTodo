using System.Xml.Linq;
using FluentAssertions;
using Xunit;

namespace MyToDo.Tests.Ui;

public sealed class UiContractTests
{
    [Fact]
    public void MainWindow_declares_required_controls_bindings_and_accessible_icon_buttons()
    {
        var path = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", "MyToDo.App", "MainWindow.xaml"));
        File.Exists(path).Should().BeTrue($"MainWindow.xaml should exist at {path}");
        var document = XDocument.Load(path);
        var names = document.Descendants().SelectMany(x => x.Attributes()).Where(a => a.Name.LocalName == "Name").Select(a => a.Value).ToHashSet(StringComparer.Ordinal);
        var required = new[] { "RootWindow", "DragRegion", "NewTodoInput", "AddTodoButton", "SearchInput", "TodoList", "HistoryButton", "TodoButton", "SettingsButton", "PinButton", "CleanHistoriesButton", "OpacitySlider", "ExitButton" };
        names.Should().Contain(required);

        var text = File.ReadAllText(path);
        text.Should().Contain("NewTodoText").And.Contain("SearchText").And.Contain("VisibleItemsView");
        foreach (var button in document.Descendants().Where(x => x.Name.LocalName == "Button"))
        {
            var hasPath = button.Descendants().Any(x => x.Name.LocalName == "Path");
            if (!hasPath) continue;
            button.Attribute("ToolTip")?.Value.Should().NotBeNullOrWhiteSpace();
            button.Attributes().Any(a => a.Name.LocalName == "AutomationProperties.Name").Should().BeTrue();
        }
    }
}
