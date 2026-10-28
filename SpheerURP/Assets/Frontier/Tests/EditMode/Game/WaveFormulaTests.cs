using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Frontier.Tests
{
    /// <summary>
    /// Wave threat formula, ported from <c>Frontier &gt; Validate Combat Balance</c> (the editor check also exercises the live
    /// <c>DefenseController</c> previews and stays as the batch gate): budgets only grow, more budget only extends the same alien
    /// sequence, and the builder respects caps and availability.
    /// </summary>
    [TestFixture]
    public class WaveFormulaTests
    {
        GameDatabase db;
        WaveFormulaSO formula;

        [OneTimeSetUp]
        public void LoadDatabase()
        {
            db = GameDatabase.Load();
            formula = db.formula;
        }

        // Same shape as DefenseController.FuelMult: more fuel means a strictly larger risk multiplier.
        static float Risk(int fuel) { return 0.5f + 0.25f * fuel; }

        [Test]
        public void AuthoredFormulaAndEnemies_Exist()
        {
            Assert.IsNotNull(formula, "the database has no wave formula");
            Assert.IsNotNull(db.enemies);
            Assert.Greater(db.enemies.Length, 0);
            Assert.Greater(db.planets.Length, 0);
        }

        [Test]
        public void Budget_GrowsWithWaveDangerAndRisk()
        {
            foreach (var planet in db.planets)
            {
                float previous = 0f;
                for (int wave = 1; wave <= 40; wave++)
                {
                    float budget = formula.Budget(wave, planet.danger, 1f);
                    Assert.GreaterOrEqual(budget, previous, planet.id + " wave " + wave + ": budget shrank");
                    Assert.Greater(formula.Budget(wave, planet.danger, 2f), budget, planet.id + " wave " + wave + ": more risk must cost more");
                    previous = budget;
                }
            }
            Assert.Greater(formula.Budget(5, 3f, 1f), formula.Budget(5, 1f, 1f), "a more dangerous planet needs a bigger budget");
        }

        [Test]
        public void Build_KeepsItsAlienSequenceWhenTheBudgetGrows()
        {
            foreach (var planet in db.planets)
            {
                for (int wave = 1; wave <= 20; wave++)
                {
                    List<EnemySO> previous = null;
                    for (int fuel = 1; fuel <= 40; fuel++)
                    {
                        var aliens = formula.Build(db.enemies, wave, formula.Budget(wave, planet.danger, Risk(fuel)), new System.Random(7919 * wave));
                        if (previous != null)
                        {
                            Assert.GreaterOrEqual(aliens.Count, previous.Count, planet.id + " wave " + wave + " fuel " + fuel + ": fewer aliens");
                            for (int i = 0; i < previous.Count; i++)
                                Assert.AreSame(previous[i], aliens[i], planet.id + " wave " + wave + " fuel " + fuel + ": alien #" + i + " was replaced");
                        }
                        previous = aliens;
                    }
                }
            }
        }

        [Test]
        public void Build_IsRepeatable()
        {
            var a = formula.Build(db.enemies, 7, 80f, new System.Random(42));
            var b = formula.Build(db.enemies, 7, 80f, new System.Random(42));
            CollectionAssert.AreEqual(a, b);
        }

        [Test]
        public void BossesAreSingularScheduledAndPaidFromTheThreatBudget()
        {
            var boss = System.Array.Find(db.enemies, e => e.rank == AlienRank.Boss);
            Assert.IsNotNull(boss);
            for (int wave = 1; wave <= 30; wave++)
            {
                var aliens = formula.Build(db.enemies, wave, 1000f, new System.Random(wave));
                Assert.AreEqual(boss.Available(wave) ? 1 : 0, aliens.FindAll(e => e.rank == AlienRank.Boss).Count, "wave " + wave);
                float spent = 0f;
                foreach (var alien in aliens) spent += alien.threat;
                Assert.LessOrEqual(spent, 1000f);
            }
            Assert.AreEqual(1, formula.Build(db.enemies, boss.minWave, 0f, new System.Random(1)).Count,
                "an unaffordable boss must not bypass the threat budget");
        }

        [Test]
        public void ArmorAppliesToAllDamageWithoutHealingOrImmunity()
        {
            var elite = System.Array.Find(db.enemies, e => e.rank == AlienRank.Elite);
            Assert.IsNotNull(elite);
            Assert.Greater(elite.armor, 0f);
            Assert.AreEqual(100f * (1f - elite.armor), elite.DamageAfterArmor(100f), 0.001f);
            Assert.AreEqual(0f, elite.DamageAfterArmor(-100f));
            Assert.Greater(elite.DamageAfterArmor(100f), 0f);
        }

        [Test]
        public void ModifiersLeaveOpeningWavesAloneAndRepeatDeterministically()
        {
            Assert.Greater(formula.modifiers.Count, 0);
            for (int wave = 1; wave < formula.modifiersFromWave; wave++) Assert.IsNull(formula.Modifier(wave));
            for (int i = 0; i < formula.modifiers.Count; i++)
            {
                var modifier = formula.Modifier(formula.modifiersFromWave + i);
                Assert.AreSame(formula.modifiers[i], modifier);
                Assert.AreSame(modifier, formula.Modifier(formula.modifiersFromWave + i + formula.modifiers.Count));
                Assert.GreaterOrEqual(modifier.reward, 1f);
            }
        }

        [Test]
        public void BossReservationHonorsTheAlienCap()
        {
            var copy = Object.Instantiate(formula);
            try
            {
                copy.maxAliens = 1;
                Assert.AreEqual(1, copy.Build(db.enemies, 10, 10000f, new System.Random(1)).Count);
            }
            finally { Object.DestroyImmediate(copy); }
        }

        [Test]
        public void Build_NeverExceedsTheAlienCapOrUsesAnUnavailableAlien()
        {
            for (int wave = 1; wave <= 30; wave++)
            {
                var aliens = formula.Build(db.enemies, wave, 1000000f, new System.Random(wave));
                Assert.LessOrEqual(aliens.Count, formula.maxAliens, "wave " + wave);
                foreach (var alien in aliens) Assert.LessOrEqual(alien.minWave, wave, alien.id + " appeared before its first wave");
            }
        }

        [Test]
        public void Build_WithNothingToSpend_StillSendsTheCheapestAlien()
        {
            var aliens = formula.Build(db.enemies, 1, 0f, new System.Random(1));
            Assert.AreEqual(1, aliens.Count);
            foreach (var alien in db.enemies)
                if (alien != null && alien.minWave <= 1) Assert.LessOrEqual(aliens[0].threat, alien.threat);
        }

        [Test]
        public void HealthRewardAndSpawnIntervals_FollowTheirCurves()
        {
            for (int wave = 1; wave < 60; wave++)
            {
                Assert.GreaterOrEqual(formula.HealthMult(wave + 1), formula.HealthMult(wave));
                Assert.GreaterOrEqual(formula.RewardMult(wave + 1, 1f), formula.RewardMult(wave, 1f));
                Assert.GreaterOrEqual(formula.RewardMult(wave, 2f), formula.RewardMult(wave, 1f));
                Assert.LessOrEqual(formula.Interval(wave + 1), formula.Interval(wave));
                Assert.GreaterOrEqual(formula.Interval(wave), 0.25f, "spawn interval fell below its floor");
            }
        }
    }
}
