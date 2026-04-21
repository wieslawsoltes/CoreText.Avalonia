using System.Diagnostics;
using System.Threading;

namespace MacOS.Avalonia;

internal sealed class MacOSDispatcherImpl : IControlledDispatcherImpl, IDispatcherImplWithExplicitBackgroundProcessing
{
    private readonly NSApplication _application;
    private readonly IntPtr _mainRunLoop;
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private readonly object _sync = new();
    private Thread? _loopThread;
    private bool _signaled;
    private bool _backgroundProcessingRequested;
    private long? _dueTimeInMs;

    public MacOSDispatcherImpl(NSApplication application)
    {
        _application = application;
        _mainRunLoop = MacOSDispatcherInterop.CFRunLoopGetMain();
    }

    public event Action? Signaled;

    public event Action? Timer;

    public event Action? ReadyForBackgroundProcessing;

    public bool CurrentThreadIsLoopThread
    {
        get
        {
            if (_loopThread is not null)
            {
                return ReferenceEquals(Thread.CurrentThread, _loopThread);
            }

            if (!NSThread.IsMain)
            {
                return false;
            }

            _loopThread = Thread.CurrentThread;
            return true;
        }
    }

    public void Signal()
    {
        lock (_sync)
        {
            _signaled = true;
        }

        MacOSDispatcherInterop.CFRunLoopWakeUp(_mainRunLoop);
    }

    public long Now => _clock.ElapsedMilliseconds;

    public void UpdateTimer(long? dueTimeInMs)
    {
        lock (_sync)
        {
            _dueTimeInMs = dueTimeInMs;
        }

        MacOSDispatcherInterop.CFRunLoopWakeUp(_mainRunLoop);
    }

    public bool CanQueryPendingInput => false;

    public bool HasPendingInput => false;

    public void RunLoop(CancellationToken token)
    {
        _loopThread ??= Thread.CurrentThread;

        while (!token.IsCancellationRequested)
        {
            if (TryDispatchManagedWork())
            {
                continue;
            }

            using var pool = new NSAutoreleasePool();
            using var untilDate = GetNextWakeDate();
            var nextEvent = _application.NextEvent((NSEventMask)ulong.MaxValue, untilDate, NSRunLoopMode.Default, true);
            if (nextEvent is null)
            {
                continue;
            }

            _application.SendEvent(nextEvent);
            _application.UpdateWindows();
        }
    }

    public void RequestBackgroundProcessing()
    {
        lock (_sync)
        {
            _backgroundProcessingRequested = true;
        }

        MacOSDispatcherInterop.CFRunLoopWakeUp(_mainRunLoop);
    }

    private bool TryDispatchManagedWork()
    {
        bool invokeSignal;
        bool invokeTimer;
        bool invokeBackground;

        lock (_sync)
        {
            invokeSignal = _signaled;
            if (invokeSignal)
            {
                _signaled = false;
            }

            invokeTimer = _dueTimeInMs is long dueTime && dueTime <= Now;
            if (invokeTimer)
            {
                _dueTimeInMs = null;
            }

            invokeBackground = !invokeSignal && !invokeTimer && _backgroundProcessingRequested;
            if (invokeBackground)
            {
                _backgroundProcessingRequested = false;
            }
        }

        if (invokeSignal)
        {
            Signaled?.Invoke();
            return true;
        }

        if (invokeTimer)
        {
            Timer?.Invoke();
            return true;
        }

        if (invokeBackground)
        {
            ReadyForBackgroundProcessing?.Invoke();
            return true;
        }

        return false;
    }

    private NSDate GetNextWakeDate()
    {
        lock (_sync)
        {
            if (_signaled || _backgroundProcessingRequested)
            {
                return NSDate.Now;
            }

            if (_dueTimeInMs is long dueTime)
            {
                var intervalMs = Math.Max(1, dueTime - Now);
                return NSDate.FromTimeIntervalSinceNow(intervalMs / 1000d);
            }
        }

        return NSDate.DistantFuture;
    }
}
