using System;
using System.Collections.Generic;
using NUnit.Framework;
using Frontier.Economy;
using Frontier.Universe;

namespace Frontier.Economy.Tests
{
    [TestFixture]
    public class CatalogSmokeTests
    {
        [Test]
        public void Builds_And_HasTheLockedCounts()
        {
            EconomyCatalog c = Fx.NewCatalog();
            Assert.AreEqual(34, c.resources.Length);
            Assert.AreEqual(31, c.structures.Length);
            Assert.AreEqual(6, c.tools.Length);
            TestLog.Info(c.resources.Length + " resources, " + c.recipes.Length + " recipes, " + c.structures.Length + " structures, " + c.upgrades.Length + " upgrades, " + c.research.Length + " research nodes");
        }
    }
}
