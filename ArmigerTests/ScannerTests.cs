using System;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ArmigerTests
{
    [TestClass]
    public class ScannerTests
    {
        [TestMethod]
        public void FindsFiles()
        {
            string rand;
            File.Move((rand = Path.GetTempFileName()), (rand = Path.ChangeExtension(rand, "dds")));

            var dir = Path.GetDirectoryName(rand);

            var recovery = new Armiger.Recovery(@"Backup\");
            var scanner = new Armiger.Scanner(dir, recovery);

            var results = scanner.ScanPattern("*");
            Assert.IsTrue(results.Contains(rand), "Scanner didn't find basedir target with wildcard pattern");
        }
    }
}
