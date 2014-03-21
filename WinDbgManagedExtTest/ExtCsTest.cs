using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ExtCS.Debugger;

namespace ExtCS.Debugger.Test
{
    [TestClass]
    public class EXTCStest
    {
        [TestMethod]
        public void TestExecute()
        {
            string testscript = "int i=2+3;System.Diagnostics.Debug.WriteLine(i);";
            //ManagedExtCS cs = ManagedExtCS.Instance();
            ManagedExtCS.Execute(testscript);
            
        }

        [TestMethod]
        public void TestExecuteForHelp()
        {
            string testscript = "-help";
            //ManagedExtCS cs = ManagedExtCS.Instance();
            ManagedExtCS.Execute(testscript);

        }

        [TestMethod]
        public void TestExecuteForPath()
        {
            string testscript = "-p:C:\testscript.csx";
            //ManagedExtCS cs = ManagedExtCS.Instance();
            ManagedExtCS.Execute(testscript);

        }
    }
}
