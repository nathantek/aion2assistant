namespace Aion2Assistant.Core.Updates;

/// <summary>
/// Évite de répéter le même avertissement à chaque cycle : seule une erreur différente de la précédente
/// (type + message) est « nouvelle ». <see cref="Reset"/> après un succès.
/// </summary>
public sealed class RepeatedFailureFilter
{
    private readonly object _gate = new();
    private string? _last;

    public bool IsNew(Exception ex)
    {
        var key = ex.GetType().FullName + "|" + ex.Message;
        lock (_gate)
        {
            if (key == _last) return false;
            _last = key;
            return true;
        }
    }

    public void Reset()
    {
        lock (_gate) _last = null;
    }
}
