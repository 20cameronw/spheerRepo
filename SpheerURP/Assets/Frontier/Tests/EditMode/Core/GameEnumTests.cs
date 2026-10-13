using System;
using NUnit.Framework;

namespace Frontier.Tests
{
    /// <summary>
    /// Enum values are serialized by number in ScriptableObject assets and save files. Appending a value is fine; reordering or
    /// removing one is a data migration. This fixture only needs Frontier.Core, so it also runs in the plain .NET CI project.
    /// </summary>
    [TestFixture]
    public class GameEnumTests
    {
        static void AssertFrozen(Type enumType, params string[] namesInOrder)
        {
            string[] actual = Enum.GetNames(enumType);
            Assert.GreaterOrEqual(actual.Length, namesInOrder.Length, enumType.Name + " lost a value");
            for (int i = 0; i < namesInOrder.Length; i++)
            {
                Assert.AreEqual(namesInOrder[i], actual[i], enumType.Name + " value #" + i + " was renamed or reordered");
                Assert.AreEqual(i, Convert.ToInt32(Enum.Parse(enumType, namesInOrder[i])), enumType.Name + "." + namesInOrder[i] + " changed its number");
            }
        }

        [Test]
        public void StructureKind_ValuesAreFrozen()
        {
            AssertFrozen(typeof(StructureKind), "Refinery", "Alarm", "Lumber", "Mine", "Drill", "Gunner", "Laser", "Satellite", "Fabricator", "Warehouse");
        }

        [Test]
        public void UpgradeEffect_ValuesAreFrozen()
        {
            AssertFrozen(typeof(UpgradeEffect), "ToolPower", "BackpackSlots", "FuelEfficiency", "TurretFireRate", "TurretDamage", "TapDamage", "ReactorHealth", "RewardBonus");
        }

        [Test]
        public void WeatherAndBackdropAndMinigameAndCategory_ValuesAreFrozen()
        {
            AssertFrozen(typeof(WeatherKind), "Clear", "Rain", "Snow", "Embers", "Spores", "Dust", "Storm");
            AssertFrozen(typeof(BackdropMood), "Auto", "Calm", "Rush", "Gloom", "Glow", "Storm");
            AssertFrozen(typeof(MinigameKind), "Chop", "Mine", "Drill");
            AssertFrozen(typeof(UpgradeCategory), "Tools", "Ship", "Defense");
        }

        [Test]
        public void StructureKinds_ClassifyGatherersTurretsAndTheirMinigame()
        {
            Assert.IsTrue(StructureKind.Lumber.IsGather());
            Assert.IsTrue(StructureKind.Mine.IsGather());
            Assert.IsTrue(StructureKind.Drill.IsGather());
            Assert.IsFalse(StructureKind.Refinery.IsGather());
            Assert.IsTrue(StructureKind.Gunner.IsTurret());
            Assert.IsTrue(StructureKind.Laser.IsTurret());
            Assert.IsFalse(StructureKind.Warehouse.IsTurret());
            Assert.AreEqual(MinigameKind.Chop, StructureKind.Lumber.Game());
            Assert.AreEqual(MinigameKind.Mine, StructureKind.Mine.Game());
            Assert.AreEqual(MinigameKind.Drill, StructureKind.Drill.Game());
        }
    }
}
