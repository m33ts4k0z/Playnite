using Playnite.Audio;

namespace Playnite.FullscreenApp.Avalonia.Services;

public sealed class FullscreenAudioService : IDisposable
{
    private readonly FullscreenSettings settings;
    private AudioEngine engine;
    private IntPtr navigationSound;
    private IntPtr activationSound;
    private IntPtr backgroundMusic;

    public bool IsAvailable => engine?.AudioInitialized == true;
    public int LoadedAssetCount { get; private set; }
    public string Status { get; private set; } = "Fullscreen audio has not been initialized.";

    public FullscreenAudioService(FullscreenSettings settings, string themeRoot)
    {
        this.settings = settings;
        Initialize(themeRoot);
    }

    public void PlayNavigation()
    {
        if (settings.AudioEnabled)
        {
            engine?.PlaySound(navigationSound);
        }
    }

    public void PlayActivation()
    {
        if (settings.AudioEnabled)
        {
            engine?.PlaySound(activationSound);
        }
    }

    public void ApplySettings()
    {
        if (!IsAvailable)
        {
            return;
        }

        if (navigationSound != IntPtr.Zero)
        {
            engine.SetSoundVolume(navigationSound, Math.Clamp(settings.InterfaceVolume, 0, 100) / 100f);
        }

        if (activationSound != IntPtr.Zero)
        {
            engine.SetSoundVolume(activationSound, Math.Clamp(settings.InterfaceVolume, 0, 100) / 100f);
        }

        engine.SetMusicVolume(Math.Clamp(settings.BackgroundVolume, 0, 100) / 100f);
        if (!settings.AudioEnabled)
        {
            engine.StopMusic();
        }
        else if (backgroundMusic != IntPtr.Zero && !engine.GetIsMusicPlaying())
        {
            engine.PlayMusic(backgroundMusic);
        }
    }

    public void Dispose()
    {
        if (engine == null)
        {
            return;
        }

        engine.StopMusic();
        if (navigationSound != IntPtr.Zero)
        {
            engine.DisposeSound(navigationSound);
        }

        if (activationSound != IntPtr.Zero)
        {
            engine.DisposeSound(activationSound);
        }

        if (backgroundMusic != IntPtr.Zero)
        {
            engine.DisposeMusic(backgroundMusic);
        }

        engine.Dispose();
        engine = null;
    }

    private void Initialize(string themeRoot)
    {
        try
        {
            engine = new AudioEngine();
            if (!engine.AudioInitialized)
            {
                Status = "SDL_mixer could not open the audio device.";
                return;
            }

            navigationSound = LoadSound(themeRoot, "navigation");
            activationSound = LoadSound(themeRoot, "activation");
            backgroundMusic = LoadMusic(themeRoot, "background");

            ApplySettings();

            Status = LoadedAssetCount == 0
                ? "SDL_mixer is ready; this theme does not provide optional audio assets."
                : $"SDL_mixer is ready with {LoadedAssetCount} theme audio assets.";
        }
        catch (Exception exception) when (
            exception is DllNotFoundException or EntryPointNotFoundException or BadImageFormatException)
        {
            Status = $"Fullscreen audio is unavailable: {exception.Message}";
            engine = null;
        }
    }

    private IntPtr LoadSound(string themeRoot, string baseName)
    {
        var path = FindAudioFile(themeRoot, baseName);
        if (path == null)
        {
            return IntPtr.Zero;
        }

        var sound = engine.LoadSound(path);
        if (sound != IntPtr.Zero)
        {
            LoadedAssetCount++;
        }

        return sound;
    }

    private IntPtr LoadMusic(string themeRoot, string baseName)
    {
        var path = FindAudioFile(themeRoot, baseName);
        if (path == null)
        {
            return IntPtr.Zero;
        }

        var music = engine.LoadMusic(path);
        if (music != IntPtr.Zero)
        {
            LoadedAssetCount++;
        }

        return music;
    }

    private static string FindAudioFile(string themeRoot, string baseName)
    {
        if (string.IsNullOrWhiteSpace(themeRoot))
        {
            return null;
        }

        var audioDirectory = Path.Combine(themeRoot, "audio");
        return AudioEngine.SupportedFileTypes
            .Select(extension => Path.Combine(audioDirectory, $"{baseName}.{extension}"))
            .FirstOrDefault(File.Exists);
    }
}
