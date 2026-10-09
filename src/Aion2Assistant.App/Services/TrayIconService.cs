using Drawing = System.Drawing;
using Forms = System.Windows.Forms;

namespace Aion2Assistant.App.Services;

/// <summary>Icône près de l'horloge : seul accès à l'app, puisque l'overlay n'apparaît ni dans la barre des tâches ni dans Alt+Tab.</summary>
internal sealed class TrayIconService : IDisposable
{
    private readonly Forms.NotifyIcon _icon;

    public TrayIconService(Action toggleVisible, Action toggleDock, Action resetPosition, Action openSettings, Action exit)
    {
        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add("Afficher / masquer", null, (_, _) => toggleVisible());
        menu.Items.Add("Ancrer / libérer", null, (_, _) => toggleDock());
        menu.Items.Add("Réinitialiser la position", null, (_, _) => resetPosition());
        menu.Items.Add("Réglages", null, (_, _) => openSettings());
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add("Quitter", null, (_, _) => exit());

        _icon = new Forms.NotifyIcon
        {
            Icon = CreateIcon(),
            Text = "Aion2Assistant",
            ContextMenuStrip = menu,
            Visible = true,
        };
        _icon.MouseClick += (_, e) =>
        {
            if (e.Button == Forms.MouseButtons.Left) toggleVisible();
        };
    }

    public void ShowWarning(string title, string text) =>
        _icon.ShowBalloonTip(5000, title, text, Forms.ToolTipIcon.Warning);

    public void Dispose()
    {
        _icon.Visible = false;
        _icon.Dispose();
    }

    /// <summary>Pastille sombre avec « A2 » en cyan, dessinée au lancement pour ne livrer aucun fichier .ico.</summary>
    private static Drawing.Icon CreateIcon()
    {
        using var bitmap = new Drawing.Bitmap(32, 32);
        using (var g = Drawing.Graphics.FromImage(bitmap))
        {
            g.SmoothingMode = Drawing.Drawing2D.SmoothingMode.AntiAlias;
            g.TextRenderingHint = Drawing.Text.TextRenderingHint.AntiAliasGridFit;
            using var background = new Drawing.SolidBrush(Drawing.Color.FromArgb(0x12, 0x14, 0x18));
            g.FillEllipse(background, 0, 0, 31, 31);
            using var font = new Drawing.Font("Segoe UI", 13, Drawing.FontStyle.Bold, Drawing.GraphicsUnit.Pixel);
            using var foreground = new Drawing.SolidBrush(Drawing.Color.FromArgb(0x22, 0xD3, 0xEE));
            var size = g.MeasureString("A2", font);
            g.DrawString("A2", font, foreground, (32 - size.Width) / 2, (32 - size.Height) / 2);
        }
        // Le handle n'est jamais libéré : une seule icône pour toute la durée de vie de l'app.
        return Drawing.Icon.FromHandle(bitmap.GetHicon());
    }
}
