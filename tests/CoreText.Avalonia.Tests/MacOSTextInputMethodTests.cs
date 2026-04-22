using Avalonia;
using Avalonia.Input.TextInput;
using MacOS.Avalonia;

namespace CoreText.Avalonia.Tests;

public sealed class MacOSTextInputMethodTests
{
    [Fact]
    public void SetClientActivatesAndRefreshesHost()
    {
        var inputMethod = new MacOSTextInputMethod();
        var host = new StubHost();
        var client = new StubClient();

        inputMethod.AttachHost(host);
        host.ResetCounters();

        inputMethod.SetClient(client);

        Assert.Same(client, inputMethod.Client);
        Assert.Equal(1, host.ActivateCount);
        Assert.Equal(1, host.RefreshCount);
    }

    [Fact]
    public void ClientEventsRefreshHostAndReplacingClientUnsubscribesOldHandlers()
    {
        var inputMethod = new MacOSTextInputMethod();
        var host = new StubHost();
        var firstClient = new StubClient();
        var secondClient = new StubClient();

        inputMethod.AttachHost(host);
        inputMethod.SetClient(firstClient);
        host.ResetCounters();

        firstClient.TriggerSelectionChanged();
        firstClient.TriggerSurroundingTextChanged();

        Assert.Equal(2, host.RefreshCount);

        inputMethod.SetClient(secondClient);
        host.ResetCounters();

        firstClient.TriggerSelectionChanged();
        secondClient.TriggerSelectionChanged();

        Assert.Equal(1, host.RefreshCount);
    }

    [Fact]
    public void SetCursorRectAndResetNotifyHost()
    {
        var inputMethod = new MacOSTextInputMethod();
        var host = new StubHost();

        inputMethod.AttachHost(host);
        host.ResetCounters();

        inputMethod.SetCursorRect(new Rect(10, 20, 30, 40));
        inputMethod.Reset();

        Assert.Equal(new Rect(10, 20, 30, 40), inputMethod.CursorRect);
        Assert.Equal(1, host.RefreshCount);
        Assert.Equal(1, host.ResetCount);
    }

    private sealed class StubHost : IMacOSTextInputHost
    {
        public int ActivateCount { get; private set; }

        public int RefreshCount { get; private set; }

        public int ResetCount { get; private set; }

        public void ActivateTextInput()
        {
            ActivateCount++;
        }

        public void RefreshTextInputState()
        {
            RefreshCount++;
        }

        public void ResetTextInputState()
        {
            ResetCount++;
        }

        public void ResetCounters()
        {
            ActivateCount = 0;
            RefreshCount = 0;
            ResetCount = 0;
        }
    }

    private sealed class StubClient : TextInputMethodClient
    {
        public override Visual TextViewVisual => null!;

        public override bool SupportsPreedit => true;

        public override bool SupportsSurroundingText => true;

        public override string SurroundingText => string.Empty;

        public override Rect CursorRectangle => default;

        public override TextSelection Selection { get; set; }

        public void TriggerSelectionChanged()
        {
            base.RaiseSelectionChanged();
        }

        public void TriggerSurroundingTextChanged()
        {
            base.RaiseSurroundingTextChanged();
        }
    }
}