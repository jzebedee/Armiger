using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;
using System.Text;
using System.IO;
using System.Threading.Tasks;
using System.Threading;
using Job = System.Collections.Generic.KeyValuePair<string, byte[]>;

namespace Armiger
{
    public class Organizer : IDisposable
    {
        BlockingCollection<Job> _jobs = new BlockingCollection<Job>();

        Task tFileReader;
        Task tFileProcessor;

        CancellationTokenSource _source = new CancellationTokenSource();

        private CancellationToken token { get { return _source.Token; } }

        public readonly ManualResetEventSlim Completed = new ManualResetEventSlim();

        public Organizer(IEnumerable<string> files, Recovery recovery)
        {
            tFileReader = Task.Factory.StartNew(() => ReadFiles(files), token);
            tFileProcessor = Task.Factory.StartNew(() => ProcessFiles(recovery), token);
        }
        ~Organizer()
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
            _source.Cancel();
        }

        protected void ReadFiles(IEnumerable<string> files)
        {
            foreach (var file in files)
            {
                if (token.IsCancellationRequested)
                    break;

                _jobs.Add(new KeyValuePair<string, byte[]>(file, File.ReadAllBytes(file)));
            }
            _jobs.CompleteAdding();
        }

        protected async void ProcessFiles(Recovery recovery)
        {
            foreach (var job in _jobs.GetConsumingEnumerable(token))
            {
                await ProcessFile(job, recovery);
            }
            Completed.Set();
        }

        protected async Task ProcessFile(Job job, Recovery recovery)
        {
            var result = await DXTManager.Instance.Process(job, recovery);

            var sb = new StringBuilder();
            sb.AppendLine("Processed " + job.Key);
            sb.AppendLine("Result: " + result.ToString());
            Console.WriteLine(sb.ToString());
        }
    }
}
