using NUnit.Framework;
using Playnite.Common;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Playnite.Tests
{
    [TestFixture]
    public class ProcessStarterTests
    {
        [Test]
        public void StartProcessWaitTest()
        {
            // cmd.exe instead of notepad: on Windows 11 notepad.exe is a Store-app
            // launcher that exits immediately, breaking pid and process-count based
            // assertions. Asserting on the returned Process object also avoids
            // counting unrelated processes already running on the machine.
            var proc = ProcessStarter.StartProcess("ping", "-t localhost");
            Assert.IsFalse(proc.HasExited);

            var ivalidRes = ProcessStarter.StartProcessWait(CmdLineTools.TaskKill, "/f /pid 999999", null, true);
            Assert.AreEqual(128, ivalidRes);

            var validRes = ProcessStarter.StartProcessWait(CmdLineTools.TaskKill, $"/f /pid {proc.Id}", null, true);
            Assert.AreEqual(0, validRes);
            Assert.IsTrue(proc.WaitForExit(3000));
        }

        [Test]
        public void ShellExecuteTest()
        {
            var procid = ProcessStarter.ShellExecute("cmd.exe");
            Assert.AreNotEqual(0, procid);
            var proc = Process.GetProcessById(procid);
            Assert.IsFalse(proc.HasExited);
            ProcessStarter.ShellExecute($"{CmdLineTools.TaskKill} /f /pid {procid}");
            Assert.IsTrue(proc.WaitForExit(3000));
        }

        [Test]
        public void StartProcessWaitStdTest()
        {
            ProcessStarter.StartProcessWait(CmdLineTools.IPConfig, null, null, out var stdOut, out var stdErr);
            StringAssert.Contains("Windows IP Configuration", stdOut);
            Assert.IsTrue(stdErr.IsNullOrEmpty());

            ProcessStarter.StartProcessWait(CmdLineTools.TaskKill, "/pid 999999", null, out var stdOut2, out var stdErr2);
            StringAssert.Contains("ERROR: The process", stdErr2);
            Assert.IsTrue(stdOut2.IsNullOrEmpty());
        }
    }
}
