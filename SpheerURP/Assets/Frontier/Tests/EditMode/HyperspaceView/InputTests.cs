using NUnit.Framework;
using UnityEngine.UIElements;
using Frontier.Hyperspace;
using Frontier.Hyperspace.View;

namespace Frontier.Tests.HyperspaceView
{
    public class InputTests
    {
        [Test]
        public void BombIsLatchedUntilASimulationStepConsumesIt()
        {
            using (var input = new HyperspaceInput(new VisualElement(), new HyperspaceTuning()))
            {
                input.PressBomb();
                Assert.IsTrue(input.Read(default).bomb);
                Assert.IsTrue(input.Read(default).bomb, "a render-only frame must not swallow a bomb");
                input.ConsumeBomb();
                Assert.IsFalse(input.Read(default).bomb);
            }
        }

        [Test]
        public void CancelClearsBombAndRecentersTargetOnTheShip()
        {
            using (var input = new HyperspaceInput(new VisualElement(), new HyperspaceTuning()))
            {
                input.Read(new PlayerState { x = 1.2f, y = -0.7f });
                input.PressBomb(); input.Cancel();
                var controls = input.Read(default);
                Assert.IsFalse(controls.bomb);
                Assert.AreEqual(1.2f, controls.targetX);
                Assert.AreEqual(-0.7f, controls.targetY);
            }
        }
    }
}
