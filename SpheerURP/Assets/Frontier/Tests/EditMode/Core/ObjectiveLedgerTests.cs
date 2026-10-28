using System;
using NUnit.Framework;

namespace Frontier.Tests
{
    public class ObjectiveLedgerTests
    {
        [Test]
        public void Normalization_MergesDuplicates_PreservesUnknownIds_AndRepairsClaimedCompletion()
        {
            var ledger = new ObjectiveLedger(new[]
            {
                null, new ObjectiveRecord(), new ObjectiveRecord { id = "future_goal", completed = true },
                new ObjectiveRecord { id = "a", completed = true }, new ObjectiveRecord { id = "a", claimed = true }
            });
            Assert.AreEqual(2, ledger.Export().Count);
            Assert.IsTrue(ledger.Completed("a"));
            Assert.IsTrue(ledger.Claimed("a"));
            Assert.IsTrue(ledger.Completed("future_goal"));
            Assert.IsFalse(ledger.TryClaim("a"));
            Assert.IsFalse(ledger.TryClaim("unknown"));
        }

        [Test]
        public void Claims_AreExactlyOnce_ThroughRepeatedObservationAndReload()
        {
            var ledger = new ObjectiveLedger();
            Assert.IsFalse(ledger.Observe("a", false));
            Assert.IsFalse(ledger.TryClaim("a"));
            Assert.IsTrue(ledger.Observe("a", true));
            Assert.IsTrue(ledger.TryClaim("a"));
            Assert.IsFalse(ledger.TryClaim("a"));
            Assert.IsFalse(ledger.Observe("a", true));
            var loaded = new ObjectiveLedger(ledger.Export());
            Assert.IsTrue(loaded.Claimed("a"));
            Assert.IsFalse(loaded.TryClaim("a"));
        }

        [Test]
        public void HistoricalBaseline_DoesNotPay_AndDoesNotBlockFutureObjectives()
        {
            var ledger = new ObjectiveLedger();
            ledger.Observe("past", true, true);
            ledger.Observe("future", false, true);
            Assert.IsFalse(ledger.TryClaim("past"));
            ledger.Observe("future", true);
            Assert.IsTrue(ledger.TryClaim("future"));
        }

        [Test]
        public void Snapshots_DoNotAliasLiveRecords_AndMissingIdsAreRejected()
        {
            var input = new ObjectiveRecord { id = "a", completed = true };
            var ledger = new ObjectiveLedger(new[] { input });
            input.claimed = true;
            var snapshot = ledger.Export();
            ledger.TryClaim("a");
            Assert.IsFalse(snapshot[0].claimed);
            snapshot[0].id = "other";
            Assert.IsTrue(ledger.Claimed("a"));
            Assert.Throws<ArgumentException>(() => ledger.Observe("", true));
        }
    }
}
