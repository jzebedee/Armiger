using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Collections.Concurrent;

namespace Armiger
{
    public class Recovery : IDisposable
    {
        const string _journalFile = ".journal";

        readonly string _backupPath, _journalPath;
        ConcurrentDictionary<string, string> _backupJournal = new ConcurrentDictionary<string, string>();

        public Recovery(string backupPath)
        {
            _backupPath = backupPath;
            _journalPath = Path.Combine(_backupPath, _journalFile);
        }
        ~Recovery()
        {
            Dispose(false);
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            FlushJournal();
        }

        public void Backup(IEnumerable<string> files)
        {
            var rand = new Random();

            foreach (var file in files)
            {
                var bkpPath = Path.Combine(_backupPath, Path.GetFileName(file));
                while (File.Exists(bkpPath))
                    bkpPath = Path.ChangeExtension(bkpPath, Path.GetExtension(bkpPath) + rand.Next(0, 10));

                if (!_backupJournal.TryAdd(file, bkpPath))
                    throw new ArgumentException();
                File.Move(file, bkpPath);
            }
        }

        public void Backup(params string[] files)
        {
            Backup(files as IEnumerable<string>);
        }

        public void RestoreFromJournal()
        {
            foreach (var line in File.ReadAllLines(_journalPath))
            {
                var groups = line.Split(new[] { "::" }, StringSplitOptions.None);

                File.Delete(groups[0]);
                File.Move(groups[1], groups[0]);//, Path.GetTempFileName());
                Console.WriteLine("Restored " + Path.GetFileNameWithoutExtension(groups[1]));
            }
        }

        public void FlushJournal()
        {
            using (var stream = File.OpenWrite(_journalPath))
            using (var writer = new StreamWriter(stream))
                foreach (var kvp in _backupJournal)
                    writer.WriteLine("{0}::{1}", kvp.Key, kvp.Value);
        }
    }
}