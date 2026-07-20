using NUnit.Framework;
using Playnite.Avalonia.App.Services;
using Playnite.SDK;
using System.Reflection;
using System.Windows;

namespace Playnite.Avalonia.App.V7.Tests;

[TestFixture]
public class AvaloniaSdkDialogRouterTests
{
    [Test]
    public void EverySdkSixDialogMemberRoutesWithoutSilentDefaults()
    {
        var dialogs = new V7PluginHostTests.TestDialogs();
        foreach (var method in typeof(IDialogsFactory).GetMethods())
        {
            var arguments = method.GetParameters().Select(CreateArgument).ToArray();
            object result = null;
            Assert.DoesNotThrow(
                () => result = AvaloniaSdkDialogRouter.Invoke(dialogs, method, arguments),
                method.ToString());
            if (method.ReturnType != typeof(void))
            {
                Assert.That(result, Is.Not.Null, method.ToString());
                Assert.That(method.ReturnType.IsInstanceOfType(result), Is.True, method.ToString());
            }
        }
    }

    [Test]
    public void AdvancedDialogValuesRoundTripThroughSdkSixRouter()
    {
        var dialogs = new V7PluginHostTests.TestDialogs();
        var inputMethod = typeof(IDialogsFactory).GetMethod(
            nameof(IDialogsFactory.SelectString),
            [typeof(string), typeof(string), typeof(string), typeof(List<MessageBoxToggle>)]);
        var toggles = new List<MessageBoxToggle> { new("Keep selection", true) };
        var input = (StringSelectionDialogResult)AvaloniaSdkDialogRouter.Invoke(
            dialogs,
            inputMethod,
            ["Message", "Caption", "seed value", toggles]);
        Assert.Multiple(() =>
        {
            Assert.That(input.Result, Is.True);
            Assert.That(input.SelectedString, Is.EqualTo("seed value"));
            Assert.That(toggles[0].Selected, Is.True);
        });

        var actionRan = false;
        var progressMethod = typeof(IDialogsFactory).GetMethod(
            nameof(IDialogsFactory.ActivateGlobalProgress),
            [typeof(Action<GlobalProgressActionArgs>), typeof(GlobalProgressOptions)]);
        var progress = (GlobalProgressResult)AvaloniaSdkDialogRouter.Invoke(
            dialogs,
            progressMethod,
            [new Action<GlobalProgressActionArgs>(_ => actionRan = true), new GlobalProgressOptions("Working")]);
        Assert.Multiple(() =>
        {
            Assert.That(actionRan, Is.True);
            Assert.That(progress.Result, Is.True);
            Assert.That(progress.Canceled, Is.False);
        });
    }

    private static object CreateArgument(ParameterInfo parameter)
    {
        var type = parameter.ParameterType;
        if (type == typeof(string))
        {
            return parameter.Name?.Contains("filter", StringComparison.OrdinalIgnoreCase) == true
                ? "Text files|*.txt"
                : "value";
        }
        if (type == typeof(bool))
        {
            return true;
        }
        if (type == typeof(double))
        {
            return parameter.DefaultValue is double value ? value : 100d;
        }
        if (type == typeof(List<MessageBoxOption>))
        {
            return new List<MessageBoxOption> { new("OK", true, true) };
        }
        if (type == typeof(List<MessageBoxToggle>))
        {
            return new List<MessageBoxToggle> { new("Toggle", true) };
        }
        if (type == typeof(List<ImageFileOption>))
        {
            return new List<ImageFileOption> { new("image.png") };
        }
        if (type == typeof(List<GenericItemOption>))
        {
            return new List<GenericItemOption> { new("Item", "Description") };
        }
        if (type == typeof(Func<string, List<GenericItemOption>>))
        {
            return new Func<string, List<GenericItemOption>>(_ =>
                new List<GenericItemOption> { new("Search result", "Description") });
        }
        if (type == typeof(Action<GlobalProgressActionArgs>))
        {
            return new Action<GlobalProgressActionArgs>(_ => { });
        }
        if (type == typeof(Func<GlobalProgressActionArgs, Task>))
        {
            return new Func<GlobalProgressActionArgs, Task>(_ => Task.CompletedTask);
        }
        if (type == typeof(GlobalProgressOptions))
        {
            return new GlobalProgressOptions("Working", true);
        }
        if (type == typeof(WindowCreationOptions))
        {
            return new WindowCreationOptions();
        }
        if (type == typeof(MessageBoxButton))
        {
            return MessageBoxButton.YesNoCancel;
        }
        if (type == typeof(MessageBoxImage))
        {
            return MessageBoxImage.Information;
        }

        throw new InvalidOperationException(
            $"No synthetic SDK dialog argument is registered for {parameter.Name}: {type}.");
    }
}
