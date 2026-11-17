// Atualização automática: consulta o versao.json publicado no GitHub, baixa o .exe novo,
// confere SHA-256 e versão, e troca o executável por renomeação (o Windows permite
// renomear um .exe em uso, mas não sobrescrever).
using System;
using System.IO;
using System.Net;
using System.Reflection;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Security.Cryptography;
using System.Text;

namespace DebloatWin.Core
{
    [DataContract]
    public class UpdateInfo
    {
        [DataMember(Name = "versao")] public string Version;
        [DataMember(Name = "url")] public string Url;
        [DataMember(Name = "sha256")] public string Sha256;
        [DataMember(Name = "notas")] public string Notes;
    }

    public static class Updater
    {
        // Canal de atualização (repositório público). Troque aqui se mudar de lugar.
        public const string ManifestUrl = "https://raw.githubusercontent.com/certificsuporte-oss/debloat-win/main/atualizacao/versao.json";

        public static string OldPath(string exePath) { return exePath + ".old"; }
        public static string NewPath(string exePath) { return exePath + ".new"; }

        public static UpdateInfo Parse(string json)
        {
            DataContractJsonSerializer ser = new DataContractJsonSerializer(typeof(UpdateInfo));
            using (MemoryStream ms = new MemoryStream(Encoding.UTF8.GetBytes(json)))
            {
                UpdateInfo u = (UpdateInfo)ser.ReadObject(ms);
                if (u == null || string.IsNullOrEmpty(u.Version) || string.IsNullOrEmpty(u.Url) || string.IsNullOrEmpty(u.Sha256))
                    throw new FormatException("versao.json incompleto");
                ParseVersion(u.Version); // valida
                if (!u.Url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                    throw new FormatException("URL de download precisa ser https");
                return u;
            }
        }

        public static Version ParseVersion(string v)
        {
            Version r = new Version(v.Trim().TrimStart('v', 'V'));
            // normaliza 1.2 → 1.2.0.0 para comparar sem surpresas
            return new Version(r.Major, r.Minor, Math.Max(0, r.Build), Math.Max(0, r.Revision));
        }

        public static bool IsNewer(string remote, string local)
        {
            return ParseVersion(remote) > ParseVersion(local);
        }

        public static string Sha256Of(string path)
        {
            using (SHA256 sha = SHA256.Create())
            using (FileStream fs = File.OpenRead(path))
            {
                byte[] h = sha.ComputeHash(fs);
                return Hex.FromBytes(h);
            }
        }

        /// <summary>Apaga o executável antigo deixado pela última atualização.</summary>
        public static void CleanupOld(string exePath)
        {
            foreach (string p in new string[] { OldPath(exePath), NewPath(exePath) })
            {
                try { if (File.Exists(p)) File.Delete(p); } catch { /* ainda em uso: tenta na próxima abertura */ }
            }
        }

        /// <summary>
        /// Confere o arquivo baixado: SHA-256 igual ao do versao.json e versão do assembly igual à anunciada.
        /// A checagem de versão evita ficar em laço de atualização se o versao.json estiver errado.
        /// </summary>
        public static string Verify(string newFile, UpdateInfo info)
        {
            string sha = Sha256Of(newFile);
            if (!string.Equals(sha, info.Sha256.Trim(), StringComparison.OrdinalIgnoreCase))
                return "SHA-256 não confere (arquivo corrompido ou alterado)";
            Version fileVersion;
            try { fileVersion = AssemblyName.GetAssemblyName(newFile).Version; }
            catch (Exception ex) { return "o arquivo baixado não é um programa .NET válido: " + ex.Message; }
            if (ParseVersion(fileVersion.ToString()) != ParseVersion(info.Version))
                return string.Format("versão do arquivo ({0}) diferente da anunciada ({1})", fileVersion, info.Version);
            return null;
        }

        /// <summary>Troca o executável em uso pelo novo. Em caso de erro, volta o original.</summary>
        public static void Swap(string exePath, string newFile)
        {
            string old = OldPath(exePath);
            if (File.Exists(old)) File.Delete(old);
            File.Move(exePath, old);
            try
            {
                File.Move(newFile, exePath);
            }
            catch
            {
                try { File.Move(old, exePath); } catch { }
                throw;
            }
        }

        // ------------------------------------------------------------------
        // Rede

        static void EnableTls12()
        {
            try { ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12; } catch { }
        }

        static HttpWebRequest Request(string url, int timeoutMs)
        {
            HttpWebRequest req = (HttpWebRequest)WebRequest.Create(url);
            req.UserAgent = AppInfo.Name.Replace(" ", "") + "/" + AppInfo.Version;
            req.Timeout = timeoutMs;
            req.ReadWriteTimeout = timeoutMs;
            req.AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate;
            req.CachePolicy = new System.Net.Cache.RequestCachePolicy(System.Net.Cache.RequestCacheLevel.NoCacheNoStore);
            return req;
        }

        /// <summary>Busca o versao.json. Devolve null se não houver versão mais nova.</summary>
        public static UpdateInfo Check(int timeoutMs)
        {
            EnableTls12();
            // parâmetro na URL para não pegar cópia velha de cache intermediário
            string url = ManifestUrl + "?t=" + DateTime.UtcNow.Ticks;
            using (WebResponse resp = Request(url, timeoutMs).GetResponse())
            using (StreamReader r = new StreamReader(resp.GetResponseStream(), Encoding.UTF8))
            {
                UpdateInfo info = Parse(r.ReadToEnd());
                return IsNewer(info.Version, AppInfo.Version) ? info : null;
            }
        }

        public delegate void DownloadProgress(long received, long total);

        public static void Download(UpdateInfo info, string target, int timeoutMs, DownloadProgress progress)
        {
            EnableTls12();
            using (WebResponse resp = Request(info.Url, timeoutMs).GetResponse())
            using (Stream s = resp.GetResponseStream())
            using (FileStream fs = File.Create(target))
            {
                long total = resp.ContentLength;
                byte[] buf = new byte[64 * 1024];
                long got = 0;
                int n;
                while ((n = s.Read(buf, 0, buf.Length)) > 0)
                {
                    fs.Write(buf, 0, n);
                    got += n;
                    if (got > 200L * 1024 * 1024) throw new IOException("download grande demais");
                    if (progress != null) progress(got, total);
                }
            }
        }
    }
}
