using GameBoyEmu.CpuNamespace;
using NUnit.Framework;
using System.Diagnostics.Contracts;
namespace GameBoyEmuTests
{
    public class Tests
    {
        [SetUp]
        public void Setup()
        {
            Cpu cpu = new GameBoyEmu.CpuNamespace.Cpu();
        }

        [Test]
        public void Test1()
        {
            Assert.AreEqual(1, 1);
        }
    }
}