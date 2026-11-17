// GERADO por tools/gerar_apps.py a partir de tools/apps.tsv. Não edite à mão.
namespace DebloatWin.Core
{
    public static partial class Catalog
    {
        static void Programas()
        {
            Category c = Category.Programas;
            T("app.googlechrome", c, Risk.Baixo, "Google Chrome", "Navegador do Google, o mais usado.").Winget("Google.Chrome", null).InGroup("Programas básicos");
            T("app.winrar", c, Risk.Baixo, "WinRAR", "Compactador clássico de arquivos RAR e ZIP.").Winget("RARLab.WinRAR", null).InGroup("Programas básicos");
            T("app.vlc", c, Risk.Baixo, "VLC", "Toca qualquer vídeo e áudio.").Winget("VideoLAN.VLC", null).InGroup("Programas básicos");
            T("app.anydesk", c, Risk.Baixo, "AnyDesk", "Acesso remoto rápido.").Winget("AnyDesk.AnyDesk", null).InGroup("Programas básicos");
            T("app.adobeacrobatreader", c, Risk.Baixo, "Adobe Acrobat Reader", "Leitor de PDF da Adobe (versão 64 bits).").Winget("Adobe.Acrobat.Reader.64-bit", null).InGroup("Programas básicos");
        }
    }
}
