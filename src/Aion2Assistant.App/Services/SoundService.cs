using System.Windows.Media;
using Aion2Assistant.Core.Audio;
using Aion2Assistant.Core.Settings;
using Serilog;

namespace Aion2Assistant.App.Services;

internal sealed class SoundService
{
    private readonly MediaPlayer _player = new();
    private string _currentPath = "";

    public SoundService(string soundsDirectory)
    {
        Directory.CreateDirectory(soundsDirectory);
        DefaultSoundPath = Path.Combine(soundsDirectory, "chime.wav");
        // Toujours réécrit : un ancien chime.wav plus faible serait sinon conservé.
        try
        {
            File.WriteAllBytes(DefaultSoundPath, WavGenerator.CreateChime());
        }
        catch (IOException ex)
        {
            Log.Warning(ex, "Impossible de régénérer le son par défaut : {Path}", DefaultSoundPath);
        }
        _player.MediaFailed += (_, e) =>
        {
            Log.Warning(e.ErrorException, "Échec de lecture du son : {Path}", _currentPath);
            if (_currentPath == DefaultSoundPath) return;
            try
            {
                _currentPath = DefaultSoundPath;
                _player.Open(new Uri(DefaultSoundPath));
                _player.Play();
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Lecture du son par défaut impossible");
            }
        };
    }

    public string DefaultSoundPath { get; }

    /// <summary>Joue le .wav choisi par l'utilisateur s'il existe toujours, sinon le son par défaut, au volume global.</summary>
    public void Play(AppSettings settings)
    {
        var path = settings.CustomSoundPath is { } custom && File.Exists(custom) ? custom : DefaultSoundPath;
        try
        {
            _currentPath = path;
            _player.Open(new Uri(path));
            _player.Volume = settings.Volume;
            _player.Play();
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Lecture du son impossible : {Path}", path);
        }
    }
}
