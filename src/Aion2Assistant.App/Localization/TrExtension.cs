using System.Windows.Markup;

namespace Aion2Assistant.App.Localization;

/// <summary>
/// Texte traduit dans le XAML : <c>xmlns:loc="clr-namespace:Aion2Assistant.App.Localization"</c> puis
/// <c>Text="{loc:Tr Cle}"</c>. Valeur lue une seule fois (la langue est fixée au démarrage, pas de changement à chaud).
/// </summary>
[MarkupExtensionReturnType(typeof(string))]
public sealed class TrExtension : MarkupExtension
{
    public TrExtension() { }

    public TrExtension(string key) => Key = key;

    [ConstructorArgument("key")]
    public string Key { get; set; } = "";

    public override object ProvideValue(IServiceProvider serviceProvider) => UiStrings.Get(Key);
}
