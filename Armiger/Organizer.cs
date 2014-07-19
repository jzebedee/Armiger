using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;
using System.Text;
using System.IO;
using System.Threading.Tasks;
using System.Threading;
using System.Diagnostics;
using Job = System.Collections.Generic.KeyValuePair<string, byte[]>;
//using JobResult = System.Tuple<string, Armiger.DXTManager.Result, byte[]>;

namespace Armiger
{
    public class Organizer : IDisposable
    {
        BlockingCollection<Job>
            _jobsIn = new BlockingCollection<Job>(),
            _jobsOut = new BlockingCollection<Job>();

        CancellationTokenSource _source = new CancellationTokenSource();

        private CancellationToken token { get { return _source.Token; } }

        public readonly ManualResetEventSlim Completed = new ManualResetEventSlim();

        public Organizer(IEnumerable<string> files, Recovery recovery)
        {
            Task.Factory.StartNew(() => ReadFiles(files), token);//.ContinueWith(t => WriteFiles(), token);
            Task.Factory.StartNew(() => WriteFiles(), token);
            Task.Factory.StartNew(() => ProcessFiles(recovery), token);
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
            if (disposing)
                Completed.Wait();
        }

        protected void ReadFiles(IEnumerable<string> files)
        {
            foreach (var file in files)
            {
                if (token.IsCancellationRequested)
                    break;

                _jobsIn.Add(new Job(file, File.ReadAllBytes(file)));
            }
            _jobsIn.CompleteAdding();
        }

        protected void WriteFiles()
        {
            foreach (var job in _jobsOut.GetConsumingEnumerable(token))
            {
                File.WriteAllBytes(job.Key, job.Value);
            }
            Completed.Set();
        }

        protected void ProcessFiles(Recovery recovery)
        {
            //Parallel.ForEach(_jobsIn.GetConsumingEnumerable(token), async job =>
            foreach (var job in _jobsIn.GetConsumingEnumerable())
            {
                try
                {
                    var jobResult = /* await */ProcessFile(job, recovery);
                    //var res = jobResult.Item2;
                    //
                    //if (res.HasFlag(DXTManager.Result.CompressedBC3))
                    //    _jobsOut.Add(new Job(jobResult.Item1, jobResult.Item3));
                }
                catch (AggregateException e)
                {
                    Trace.TraceError(e.ToString());
                    continue;
                }
            }//);
            _jobsOut.CompleteAdding();
        }

        protected async Task/*<JobResult>*/ ProcessFile(Job job, Recovery recovery)
        {
            //var result = await DXTManager.Instance.Process(job, recovery);
            var proc = StartNVCompress(job.Key, recovery.GetBackupPath(job.Key));

            var sb = new StringBuilder();
            sb.AppendLine("Processed " + job.Key);
            //sb.AppendLine("Result: " + result.Item2.ToString());
            Console.WriteLine(sb.ToString());
        }

        private const string _nvttPath = @"C:\Program Files\NVIDIA Corporation\NVIDIA Texture Tools 2\nvttcmd.bat";
        private Process StartNVCompress(string infile, string outfile)
        {
            var proc = new Process()
            {
                StartInfo = new ProcessStartInfo(_nvttPath, string.Format(@"nvcompress {2} ""{0}"" ""{1}""", Path.GetFullPath(infile), Path.GetFullPath(outfile), "-bc5")),
            };
            proc.OutputDataReceived += (sender, e) => Trace.TraceInformation(e.Data);
            proc.Start();

            return proc;
        }
    }
}
