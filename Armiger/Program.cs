using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Diagnostics;

namespace Armiger
{
    class Program
    {
        static void Main(string[] args)
        {
            try
            {
                Trace.Listeners.Add(new ConsoleTraceListener(false));

                var recov = new Recovery(@"..\Release\Backup\-8588327262962227421\");
                recov.RestoreFromJournal();
                Console.ReadKey();
                return;

                Console.WriteLine("Armiger");
                PrintBorder();

                if (args.Length < 3)
                {
                    Console.WriteLine("Expected arguments not found.");
                    Console.WriteLine(@"Example: armiger ""c:\root\path"" ""c:\backup\path"" arguments");
                    return;
                }

                var root_path = args[0];
                var bckp_path = args[1];
                var oper_args = args[2];
                Operate(root_path, bckp_path, oper_args);
            }
            finally
            {
                PrintBorder();
                Console.WriteLine("Execution complete.");
                Console.ReadKey();
            }
        }

        static void PrintBorder(char border = '-', int times = 40)
        {
            Console.WriteLine(new String(border, times));
        }

        static void Operate(string root, string backup, string operation)
        {
            var backupDI = Directory.CreateDirectory(Path.Combine(backup, DateTime.Now.ToBinary() + @"\"));
            var recovery = new Recovery(backupDI.FullName);

            var scanner = new Scanner(root, recovery);
            foreach (var kvp in scanner.RemovePattern("thumbs.db", "pspbrwse.jbf", "*.tmp"))
            {
                Console.WriteLine("Removing flotsam " + kvp.Key);
                kvp.Value();
            }

            var fileTasks = new List<Task>();
            foreach (var file in scanner.ScanPattern("*.dds"))
            {
                fileTasks.Add(ProcessFile(file, recovery));
            }
            Task.WhenAll(fileTasks).Wait();

            Console.WriteLine("DXT Processing complete. Press any key to proceed to duplicate removal.");
            Console.ReadKey();

            foreach (var kvp in scanner.RemoveDuplicates("*.dds"))
            {
                Console.WriteLine("Removing duplicates of " + kvp.Key);
                kvp.Value();
            }

            recovery.FlushJournal();
            Console.WriteLine("Good.");
        }

        static async Task ProcessFile(string file, Recovery recovery)
        {
            var result = await DXTManager.Instance.Process(file, recovery);
            var sb = new StringBuilder();
            sb.AppendLine("Checked " + file);
            sb.AppendLine("Result: " + result.ToString());
            Console.WriteLine(sb.ToString());
        }
    }
}
