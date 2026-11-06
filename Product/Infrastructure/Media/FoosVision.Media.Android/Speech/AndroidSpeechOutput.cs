// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using Android.Content;
using Android.Content.Res;
using Android.Media;
using Android.OS;
using Android.Speech.Tts;
using FoosVision.Common.Logging;
using FoosVision.Media.Core.Speech;
using FoosVision.Ports.Media;

namespace FoosVision.Media.Android.Speech;

/// <summary>Recorder-local recordings with default Android TTS for unknown texts.</summary>
public class AndroidSpeechOutput : ISpeechOutput, IDisposable
{
    private static readonly Source _Log = new("Android.Speech");
    private readonly Lock _Gate = new();
    private readonly Context _Context;
    private readonly AudioManager _AudioManager;
    private readonly AudioAttributes _Attributes;
    private readonly ProgressListener _Progress;
    private readonly InitListener _Init = new();
    private readonly Handler _Handler = new(Looper.MainLooper!);
    private TextToSpeech? _Engine;
    private Task<SpeechOutputResult>? _Preparation;
    private Utterance? _Current;
    private volatile bool _Foreground;
    private volatile bool _Disposed;

    public AndroidSpeechOutput(Context context)
    {
        _Context = context.ApplicationContext!;
        _AudioManager = (AudioManager)_Context.GetSystemService(Context.AudioService)!;
        _Attributes = new AudioAttributes.Builder()!
            .SetUsage(AudioUsageKind.Media)!
            .SetContentType(AudioContentType.Speech)!
            .Build()!;
        _Progress = new ProgressListener(this);
    }

    public event Action? Interrupted;

    public bool IsOutputAllowed => _Foreground && !_Disposed;

    public void SetForeground(bool foreground)
    {
        lock (_Gate)
        {
            _Foreground = foreground;
            if (foreground || _Disposed) return;
            StopCurrent(SpeechOutputResult.Cancelled);
        }

        Interrupted?.Invoke();
    }

    public Task<SpeechOutputResult> PrepareAsync(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        // Bundled recordings do not depend on a TTS engine or installed voices.
        return Task.FromResult(_Disposed ? SpeechOutputResult.Unavailable : SpeechOutputResult.Ready);
    }

    private async Task<SpeechOutputResult> PrepareTextToSpeechAsync(CancellationToken ct)
    {
        Task<SpeechOutputResult> preparation;
        lock (_Gate)
        {
            if (_Disposed) return SpeechOutputResult.Unavailable;
            preparation = _Preparation ??= InitializeAsync();
        }

        return await preparation.WaitAsync(ct).ConfigureAwait(false);
    }

    private async Task<SpeechOutputResult> InitializeAsync()
    {
        try
        {
            _Engine = new TextToSpeech(_Context, _Init);
            OperationResult status = await _Init.Completion.Task.ConfigureAwait(false);
            lock (_Gate)
            {
                if (_Disposed) return SpeechOutputResult.Unavailable;
                if (status != OperationResult.Success)
                {
                    return Failure(SpeechOutputResult.Unavailable, "TTS initialization failed.");
                }

                Voice? voice = _Engine.DefaultVoice;
                if (voice is null)
                {
                    return Failure(SpeechOutputResult.LanguageUnavailable, "The default TTS voice is unavailable.");
                }

                if (_Engine.SetVoice(voice) != OperationResult.Success ||
                    _Engine.SetAudioAttributes(_Attributes) != OperationResult.Success ||
                    _Engine.SetOnUtteranceProgressListener(_Progress) != OperationResult.Success)
                {
                    return Failure(SpeechOutputResult.Failed, "TTS voice or callback configuration failed.");
                }

                _Log.Information("Recorder default TTS prepared. Voice={Voice}", voice.Name);
                return SpeechOutputResult.Ready;
            }
        }
        catch (Exception ex)
        {
            _Log.Error("TTS preparation failed.", ex);
            return SpeechOutputResult.Unavailable;
        }
    }

    public async Task<SpeechOutputResult> SpeakAsync(string text, CancellationToken ct)
    {
        string? recording = SpeechRecordingCatalog.Find(text);
        if (recording is null)
        {
            SpeechOutputResult prepared = await PrepareTextToSpeechAsync(ct).ConfigureAwait(false);
            if (prepared != SpeechOutputResult.Ready) return prepared;
        }

        Utterance utterance;
        lock (_Gate)
        {
            if (ct.IsCancellationRequested) return SpeechOutputResult.Cancelled;
            if (_Disposed || !_Foreground) return SpeechOutputResult.Unavailable;

            StopCurrent(SpeechOutputResult.Cancelled);
            utterance = new Utterance(this, _Attributes, _Handler, recording is not null);
            _Current = utterance;
            try
            {
                if (_AudioManager.RequestAudioFocus(utterance.FocusRequest) != AudioFocusRequest.Granted)
                {
                    Complete(utterance.Id, Failure(SpeechOutputResult.AudioFocusDenied, "Speech audio focus denied."));
                }
                else if (_Current == utterance)
                {
                    _Handler.Post(() => BeginOutput(utterance, recording, text, ct));
                }
            }
            catch (Exception ex)
            {
                _Log.Error("Speech request failed.", ex);
                StopCurrent(SpeechOutputResult.Failed);
            }
        }

        // Invalidation is handed off by the frame presenter; never make a native Stop call there.
        using CancellationTokenRegistration registration = ct.Register(() =>
            ThreadPool.QueueUserWorkItem(_ => Cancel(utterance.Id)));
        return await utterance.Completion.Task.ConfigureAwait(false);
    }

    private void BeginOutput(Utterance utterance, string? recording, string text, CancellationToken ct)
    {
        lock (_Gate)
        {
            if (_Current != utterance) return;
            if (ct.IsCancellationRequested)
            {
                StopCurrent(SpeechOutputResult.Cancelled);
                return;
            }

            try
            {
                if (recording is not null)
                {
                    PlayRecording(utterance, recording);
                    return;
                }

                if (_Engine!.Speak(text, QueueMode.Flush, null, utterance.Id) != OperationResult.Success)
                {
                    StopCurrent(Failure(SpeechOutputResult.Failed, "TTS rejected speech request."));
                }
            }
            catch (Exception ex)
            {
                _Log.Error("Starting speech output failed.", ex);
                StopCurrent(SpeechOutputResult.Failed);
            }
        }
    }

    private void PlayRecording(Utterance utterance, string recording)
    {
        MediaPlayer player = new();
        utterance.Player = player;
        player.SetAudioAttributes(_Attributes);
        player.Prepared += (_, _) =>
        {
            lock (_Gate)
            {
                if (_Current != utterance) return;
                try
                {
                    player.Start();
                }
                catch (Exception ex)
                {
                    _Log.Error("Starting speech recording failed.", ex);
                    Complete(utterance.Id, SpeechOutputResult.Failed);
                }
            }
        };
        player.Completion += (_, _) => Complete(utterance.Id, SpeechOutputResult.Completed);
        player.Error += (_, error) =>
        {
            error.Handled = true;
            Complete(utterance.Id, Failure(SpeechOutputResult.Failed,
                $"Speech recording failed: {recording}, {error.What}/{error.Extra}."));
        };

        using AssetFileDescriptor descriptor = _Context.Resources!.OpenRawResourceFd(RecordingResourceId(recording))!;
        player.SetDataSource(descriptor.FileDescriptor, descriptor.StartOffset, descriptor.Length);
        player.PrepareAsync();
    }

    private static int RecordingResourceId(string recording) => recording switch
    {
        "speech_left" => Resource.Raw.speech_left,
        "speech_right" => Resource.Raw.speech_right,
        "speech_middle" => Resource.Raw.speech_middle,
        "speech_short" => Resource.Raw.speech_short,
        "speech_long" => Resource.Raw.speech_long,
        "speech_wall" => Resource.Raw.speech_wall,
        "speech_lane" => Resource.Raw.speech_lane,
        "speech_split" => Resource.Raw.speech_split,
        "speech_left_ready" => Resource.Raw.speech_left_ready,
        "speech_right_ready" => Resource.Raw.speech_right_ready,
        "speech_middle_ready" => Resource.Raw.speech_middle_ready,
        "speech_short_ready" => Resource.Raw.speech_short_ready,
        "speech_long_ready" => Resource.Raw.speech_long_ready,
        "speech_wall_ready" => Resource.Raw.speech_wall_ready,
        "speech_lane_ready" => Resource.Raw.speech_lane_ready,
        "speech_split_ready" => Resource.Raw.speech_split_ready,
        "speech_snake_ready" => Resource.Raw.speech_snake_ready,
        "speech_pull_ready" => Resource.Raw.speech_pull_ready,
        "speech_pass_ready" => Resource.Raw.speech_pass_ready,
        "speech_shoot" => Resource.Raw.speech_shoot,
        "speech_aborted" => Resource.Raw.speech_aborted,
        _ => throw new ArgumentOutOfRangeException(nameof(recording)),
    };

    private void Cancel(string id)
    {
        lock (_Gate)
        {
            if (_Current?.Id == id) StopCurrent(SpeechOutputResult.Cancelled);
        }
    }

    private void OnFocusChanged(string id, AudioFocus focus)
    {
        if (focus == AudioFocus.Gain) return;
        lock (_Gate)
        {
            if (_Current?.Id != id) return;
            StopCurrent(SpeechOutputResult.Cancelled);
        }

        Interrupted?.Invoke();
    }

    private void StopCurrent(SpeechOutputResult result)
    {
        if (_Current is not { } current) return;
        try
        {
            if (!current.IsRecording && _Engine?.Stop() == OperationResult.Error)
            {
                Failure(SpeechOutputResult.Failed, "TTS could not stop speech.");
            }
        }
        catch (Exception ex)
        {
            _Log.Error("TTS stop failed.", ex);
        }
        finally
        {
            Complete(current.Id, result);
        }
    }

    private void Complete(string? id, SpeechOutputResult result)
    {
        lock (_Gate)
        {
            if (_Current is not { } current || current.Id != id) return;
            _Current = null;
            if (current.Player is not null && Looper.MyLooper() != Looper.MainLooper)
            {
                // MediaPlayer creation, callbacks and release all run on the same Looper.
                _Handler.Post(() => ReleaseOutput(current, result));
                return;
            }

            ReleaseOutput(current, result);
        }
    }

    private void ReleaseOutput(Utterance current, SpeechOutputResult result)
    {
        try
        {
            current.Player?.Release();
            current.Player?.Dispose();
            _AudioManager.AbandonAudioFocusRequest(current.FocusRequest);
        }
        catch (Exception ex)
        {
            _Log.Error("Releasing speech output failed.", ex);
        }
        finally
        {
            current.Completion.TrySetResult(result);
            current.FocusRequest.Dispose();
            current.Listener.Dispose();
        }
    }

    private static SpeechOutputResult Failure(SpeechOutputResult result, string message)
    {
        _Log.Warning("{Message} Result={Result}", message, result);
        return result;
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
        lock (_Gate)
        {
            if (_Disposed) return;
            _Disposed = true;
            StopCurrent(SpeechOutputResult.Cancelled);
            _Init.Completion.TrySetResult(OperationResult.Error);
            _Engine?.Shutdown();
            _Engine?.Dispose();
            _Progress.Dispose();
            _Init.Dispose();
            _Attributes.Dispose();
            _Handler.Dispose();
        }
    }

    private sealed class InitListener : Java.Lang.Object, TextToSpeech.IOnInitListener
    {
        public TaskCompletionSource<OperationResult> Completion { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public void OnInit(OperationResult status) => Completion.TrySetResult(status);
    }

    private sealed class ProgressListener(AndroidSpeechOutput owner) : UtteranceProgressListener
    {
        public override void OnStart(string? utteranceId) { }
        public override void OnDone(string? utteranceId) => owner.Complete(utteranceId, SpeechOutputResult.Completed);
        public override void OnStop(string? utteranceId, bool interrupted) => owner.Complete(utteranceId, SpeechOutputResult.Cancelled);
        [Obsolete("Required legacy Android TTS callback.")]
        public override void OnError(string? utteranceId) => owner.Complete(utteranceId,
            Failure(SpeechOutputResult.Failed, "TTS utterance failed."));
        public override void OnError(string? utteranceId, TextToSpeechError errorCode) => owner.Complete(utteranceId,
            Failure(SpeechOutputResult.Failed, $"TTS utterance failed: {errorCode}."));
    }

    private sealed class FocusListener(AndroidSpeechOutput owner, string id) : Java.Lang.Object, AudioManager.IOnAudioFocusChangeListener
    {
        public void OnAudioFocusChange(AudioFocus focusChange) => owner.OnFocusChanged(id, focusChange);
    }

    private sealed class Utterance
    {
        public string Id { get; } = Guid.NewGuid().ToString("N");
        public TaskCompletionSource<SpeechOutputResult> Completion { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public FocusListener Listener { get; }
        public AudioFocusRequestClass FocusRequest { get; }
        public bool IsRecording { get; }
        public MediaPlayer? Player { get; set; }

        public Utterance(AndroidSpeechOutput owner, AudioAttributes attributes, Handler handler, bool isRecording)
        {
            IsRecording = isRecording;
            Listener = new FocusListener(owner, Id);
            FocusRequest = new AudioFocusRequestClass.Builder(AudioFocus.GainTransient)!
                .SetAudioAttributes(attributes)!
                .SetAcceptsDelayedFocusGain(false)!
                .SetWillPauseWhenDucked(true)!
                .SetOnAudioFocusChangeListener(Listener, handler)!
                .Build()!;
        }
    }
}
