// Modelo de dados do catálogo de ajustes.
// Código escrito para C# 5 / .NET Framework 4.8, para poder ser recompilado
// com o csc.exe que já vem no Windows (ver build.bat).
using System;
using System.Collections.Generic;

namespace DebloatWin.Core
{
    public static class AppInfo
    {
        // Troque aqui para renomear o programa.
        public const string Name = "Debloat Win";
        public const string Version = "1.6.0";
        public const string DataFolderName = "DebloatWin";
    }

    public enum Risk
    {
        Baixo = 1,
        Medio = 2,
        Alto = 3
    }

    [Flags]
    public enum WinVersion
    {
        Win10 = 1,
        Win11 = 2,
        Ambos = Win10 | Win11
    }

    public enum Category
    {
        Apps,
        Privacidade,
        Telemetria,
        Atualizacoes,
        Desempenho,
        Jogos,
        Interface,
        Ferramentas,
        Programas
    }

    public static class CategoryInfo
    {
        public static string Title(Category c)
        {
            switch (c)
            {
                case Category.Apps: return "Apps e recursos";
                case Category.Privacidade: return "Privacidade";
                case Category.Telemetria: return "Telemetria";
                case Category.Atualizacoes: return "Windows Update";
                case Category.Desempenho: return "Desempenho";
                case Category.Jogos: return "Jogos";
                case Category.Interface: return "Interface";
                case Category.Ferramentas: return "Ferramentas";
                case Category.Programas: return "Instalar programas";
            }
            return c.ToString();
        }

        public static string Subtitle(Category c)
        {
            switch (c)
            {
                case Category.Apps: return "Remove apps pré-instalados, IA e recursos que vêm ligados de fábrica.";
                case Category.Privacidade: return "Anúncios, sugestões, histórico e permissões de apps.";
                case Category.Telemetria: return "Coleta de dados do Windows e de programas de terceiros.";
                case Category.Atualizacoes: return "Controle de quando e como o Windows Update age.";
                case Category.Desempenho: return "Energia, serviços, efeitos visuais e inicialização.";
                case Category.Jogos: return "Ajustes voltados a jogos e latência.";
                case Category.Interface: return "Explorer, barra de tarefas e preferências visuais.";
                case Category.Ferramentas: return "Manutenção e reparo. Executam uma vez e não têm desfazer.";
                case Category.Programas: return "Instalados pelo winget, na versão mais nova. Desfazer só remove o que este app instalou.";
            }
            return "";
        }
    }

    /// <summary>
    /// Um ajuste do catálogo: o que o usuário marca na tela.
    /// </summary>
    public class Tweak
    {
        public string Id;
        public Category Category;
        public string Title;
        public string Description;
        public string Warning;            // aviso exibido em destaque (opcional)
        public string Group;              // subgrupo dentro da categoria (usado em Programas)
        public Risk Risk = Risk.Baixo;
        public WinVersion Windows = WinVersion.Ambos;
        public bool Padrao;               // faz parte do preset Padrão (lista em Catalog.PadraoIds)
        public bool NeedsRestart;
        public bool ProOnly;              // política que só vale no Pro/Enterprise
        public List<TweakAction> Actions = new List<TweakAction>();

        public bool IsReversible
        {
            get
            {
                foreach (TweakAction a in Actions)
                    if (!a.CanUndo) return false;
                return true;
            }
        }

        public bool AppliesTo(WinVersion current)
        {
            return (Windows & current) != 0;
        }

    }

    public enum ActionOutcome
    {
        Ok,
        Skipped,   // não se aplica (ex.: app não instalado, tarefa inexistente)
        Failed
    }

    public class ActionResult
    {
        public ActionOutcome Outcome;
        public string Message;
        public UndoRecord Undo;   // null se não há o que desfazer

        public static ActionResult Ok(string msg, UndoRecord undo)
        {
            ActionResult r = new ActionResult();
            r.Outcome = ActionOutcome.Ok; r.Message = msg; r.Undo = undo;
            return r;
        }

        public static ActionResult Skip(string msg)
        {
            ActionResult r = new ActionResult();
            r.Outcome = ActionOutcome.Skipped; r.Message = msg;
            return r;
        }

        public static ActionResult Fail(string msg)
        {
            ActionResult r = new ActionResult();
            r.Outcome = ActionOutcome.Failed; r.Message = msg;
            return r;
        }
    }

    /// <summary>Situação de uma ação neste PC.</summary>
    public enum DetectState
    {
        Unknown,        // não dá para saber (ex.: limpezas, que não deixam rastro)
        NotApplied,
        Applied,
        NotApplicable   // não existe neste PC (programa, serviço ou recurso ausente)
    }

    /// <summary>
    /// Uma operação concreta no sistema. Cada ajuste tem uma ou mais.
    /// </summary>
    public abstract class TweakAction
    {
        public abstract bool CanUndo { get; }
        public abstract string Describe();
        public abstract ActionResult Apply(ISystem sys);
        /// <summary>Equivalente em PowerShell, para exportar o script.</summary>
        public abstract string ToPowerShell();

        /// <summary>Verificação rápida, sem PowerShell (registro). Unknown = usar DetectScript.</summary>
        public virtual DetectState Detect(ISystem sys) { return DetectState.Unknown; }

        /// <summary>
        /// Trecho PowerShell que devolve '1' (aplicado), '0' (não aplicado) ou 'NA' (não existe neste PC).
        /// Todos os trechos rodam juntos numa única execução. null = não tem verificação.
        /// </summary>
        public virtual string DetectScript() { return null; }
    }
}
