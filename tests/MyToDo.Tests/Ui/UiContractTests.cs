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
        var required = new[] { "RootWindow", "DragRegion", "NewTodoInput", "AddTodoButton", "SearchInput", "ErrorMessageText", "TodoList", "HistoryButton", "TodoButton", "SettingsButton", "PinButton", "CleanHistoriesButton", "OpacitySlider", "ExitButton" };
        names.Should().Contain(required);

        var text = File.ReadAllText(path);
        text.Should().Contain("NewTodoText").And.Contain("SearchText").And.Contain("VisibleItemsView");
        var contentTextBlocks = document.Descendants().Where(x => x.Name.LocalName == "TextBlock" && x.Attributes().Any(a => a.Name.LocalName == "Text" && a.Value.Contains("{Binding Content}", StringComparison.Ordinal))).ToArray();
        contentTextBlocks.Should().Contain(x => x.Attributes().Any(a => a.Name.LocalName == "TextDecorations"));
        var codeBehind = File.ReadAllText(Path.Combine(Path.GetDirectoryName(path)!, "MainWindow.xaml.cs"));
        codeBehind.Should().NotContain("Window_Closing");
        codeBehind.Should().NotContain("async void ExitButton_Click").And.NotContain("ExitButton_Click(object sender, RoutedEventArgs e) { await PersistSettingsAsync");
        var appCode = File.ReadAllText(Path.Combine(Path.GetDirectoryName(path)!, "App.xaml.cs"));
        appCode.Should().NotContain("async void OnExit");
        appCode.Should().Contain("PersistSettingsAsync().GetAwaiter().GetResult()");
        appCode.Should().Contain("finally");
        text.Should().Contain("x:Name=\"TodoText\"").And.Contain("x:Name=\"EditTodoText\"");
        text.Should().Contain("WindowChrome.IsHitTestVisibleInChrome=\"True\"");
        text.Should().Contain("Text=\"{Binding ErrorMessage}\"");
        text.Should().Contain("KeyDown=\"TodoText_KeyDown\"");
        text.Should().Contain("KeyDown=\"TodoList_KeyDown\"");
        text.Should().Contain("Value=\"{Binding IsCompleted, Mode=OneWay}\"");
        var historyText = document.Descendants().Single(x => x.Attributes().Any(a => a.Name.LocalName == "Name" && a.Value == "HistoryText"));
        historyText.Attribute("TextWrapping")?.Value.Should().Be("Wrap");
        var todoText = document.Descendants().Single(x => x.Attributes().Any(a => a.Name.LocalName == "Name" && a.Value == "TodoText"));
        todoText.Attribute("Focusable")?.Value.Should().Be("True");
        codeBehind.Should().NotContain("_persistTask");
        codeBehind.Should().Contain("PersistSettingsAsync");
        codeBehind.Should().Contain("await PersistSettingsAsync()");
        text.Should().Contain("<MultiDataTrigger>");
        text.Should().Contain("Binding=\"{Binding IsEditing}\" Value=\"False\"");
        text.Should().Contain("Binding=\"{Binding IsEditing}\" Value=\"True\"");
        text.Should().Contain("Binding=\"{Binding DataContext.CurrentPage, RelativeSource={RelativeSource AncestorType=Window}}\" Value=\"{x:Static vm:AppPage.ToDo}\"");
        text.Should().Contain("ConverterParameter=History");
        foreach (var button in document.Descendants().Where(x => x.Name.LocalName == "Button"))
        {
            var hasPath = button.Descendants().Any(x => x.Name.LocalName == "Path");
            if (!hasPath) continue;
            button.Attribute("ToolTip")?.Value.Should().NotBeNullOrWhiteSpace();
            button.Attributes().Any(a => a.Name.LocalName == "AutomationProperties.Name").Should().BeTrue();
        }
    }
}
