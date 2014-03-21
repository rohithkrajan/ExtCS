using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ExtCS.Debugger;
using Moq;
using DotNetDbg;
namespace ExtCS.Debugger.Test
{
    [TestClass]
    public class ManagedExtCsTest
    {
        [TestMethod]
        public void TestExecute()
        {
            Mock<IDebugClient> mockClient=new Mock<IDebugClient>();
            
            Debugger debug = new Debugger(mockClient.Object);

            
        }
    }
}
