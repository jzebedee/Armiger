using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Armiger
{
    public class Scanner : IDisposable
    {
        string _rootPath;

        Recovery _recovery;

        //backup cannot be a subdirectory of rootpath
        public Scanner(string root, string backup)
        {
            if (!Directory.Exists(root))
                throw new ArgumentException("Root directory does not exist", "root");

            var backupDI = Directory.CreateDirectory(Path.Combine(backup, DateTime.Now.ToBinary() + @"\"));
            _recovery = new Recovery(backupDI.FullName);

            _rootPath = root;
        }

        public virtual void Dispose()
        {
            _recovery.Dispose();
        }

        public IEnumerable<string> ScanPattern(params string[] patterns)
        {
            return (from pattern in patterns
                    select Directory.EnumerateFiles(_rootPath, pattern, SearchOption.AllDirectories)).SelectMany(f => f);
        }

        public IDictionary<string, IEnumerable<string>> ScanDuplicates(string dominantExtensionPattern)
        {
            var ext = Path.GetExtension(dominantExtensionPattern);
            return
                (from pat in ScanPattern(dominantExtensionPattern)
                 let others = Directory.EnumerateFiles(Path.GetDirectoryName(pat), Path.GetFileNameWithoutExtension(pat) + ".*", SearchOption.TopDirectoryOnly).
                     Where(f => !Path.GetExtension(f).Equals(ext, StringComparison.InvariantCultureIgnoreCase))
                 where others.Count() > 0
                 select new KeyValuePair<string, IEnumerable<string>>(pat, others)).ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
        }

        public IEnumerable<KeyValuePair<string, Action>> RemovePattern(params string[] patterns)
        {
            return ScanPattern(patterns).Select(path => new KeyValuePair<string, Action>(path, () => _recovery.Backup(path)));
        }

        public IEnumerable<KeyValuePair<string, Action>> RemoveDuplicates(string dominantExtensionPattern)
        {
            return ScanDuplicates(dominantExtensionPattern).Select(kvp => new KeyValuePair<string, Action>(kvp.Key, () => _recovery.Backup(kvp.Value)));
        }
    }
}
