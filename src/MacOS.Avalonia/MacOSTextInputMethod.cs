using Avalonia.Input.TextInput;

namespace MacOS.Avalonia;

internal interface IMacOSTextInputHost
{
    void ActivateTextInput();

    void RefreshTextInputState();

    void ResetTextInputState();
}

internal sealed class MacOSTextInputMethod : ITextInputMethodImpl, IDisposable
{
    private IMacOSTextInputHost? _host;
    private TextInputMethodClient? _client;
    private Rect _cursorRect;
    private TextInputOptions _options = TextInputOptions.Default;

    public TextInputMethodClient? Client => _client;

    public Rect CursorRect => _cursorRect;

    public TextInputOptions Options => _options;

    public bool HasClient => _client is not null;

    public void AttachHost(IMacOSTextInputHost host)
    {
        _host = host;
        if (_client is not null)
        {
            _host.ActivateTextInput();
        }

        _host.RefreshTextInputState();
    }

    public void DetachHost(IMacOSTextInputHost host)
    {
        if (ReferenceEquals(_host, host))
        {
            _host = null;
        }
    }

    public void SetClient(TextInputMethodClient? client)
    {
        if (ReferenceEquals(_client, client))
        {
            return;
        }

        if (_client is not null)
        {
            _client.TextViewVisualChanged -= OnClientStateChanged;
            _client.CursorRectangleChanged -= OnClientStateChanged;
            _client.SurroundingTextChanged -= OnClientStateChanged;
            _client.SelectionChanged -= OnClientStateChanged;
        }

        _client = client;

        if (_client is not null)
        {
            _client.TextViewVisualChanged += OnClientStateChanged;
            _client.CursorRectangleChanged += OnClientStateChanged;
            _client.SurroundingTextChanged += OnClientStateChanged;
            _client.SelectionChanged += OnClientStateChanged;
            _host?.ActivateTextInput();
        }
        else
        {
            _host?.ResetTextInputState();
        }

        _host?.RefreshTextInputState();
    }

    public void SetCursorRect(Rect rect)
    {
        _cursorRect = rect;
        _host?.RefreshTextInputState();
    }

    public void SetOptions(TextInputOptions options)
    {
        _options = options ?? TextInputOptions.Default;
        _host?.RefreshTextInputState();
    }

    public void Reset()
    {
        _host?.ResetTextInputState();
    }

    public void Dispose()
    {
        SetClient(null);
        _host = null;
    }

    private void OnClientStateChanged(object? sender, EventArgs e)
    {
        _host?.RefreshTextInputState();
    }
}