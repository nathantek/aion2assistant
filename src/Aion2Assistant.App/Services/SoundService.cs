using System.Windows.Media;
using Aion2Assistant.Core.Audio;
using Serilog;

namespace Aion2Assistant.App.Services;

internal sealed class SoundService
{
    private readonly MediaPlayer _player = new();
    private readonly string _soundsDirectory;
    private string _currentPath = "";

    public SoundService(string soundsDirectory)
    {
        Directory.CreateDirectory(soundsDirectory);
        _soundsDirectory = soundsDirectory;
        DefaultSoundPath = Path.Combine(soundsDirectory, SoundResolver.FileName(AlertSounds.DefaultId));
        // Toujours réécrits : un ancien fichier plus faible serait sinon conservé.
        foreach (var sound in AlertSounds.BuiltIn)
        {
            var path = Path.Combine(soundsDirectory, SoundResolver.FileName(sound.Id));
            try
            {
                File.WriteAllBytes(path, AlertSounds.Create(sound.Id));
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                Log.Warning(ex, "Impossible de régénérer le son intégré : {Path}", path);
            }
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

    /// <summary>Joue le son choisi (intégré ou fichier perso s'il existe toujours), sinon le carillon, au volume donné.</summary>
    public void Play(SoundRef sound, double volume)
    {
        var path = SoundResolver.Resolve(sound.Id, sound.CustomPath, _soundsDirectory, File.Exists);
        try
        {
            _currentPath = path;
            _player.Open(new Uri(path));
            _player.Volume = volume;
            _player.Play();
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Lecture du son impossible : {Path}", path);
        }
    }
}
