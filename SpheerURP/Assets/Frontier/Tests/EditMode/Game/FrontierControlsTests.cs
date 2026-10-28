using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Frontier.Tests
{
    public sealed class FrontierControlsTests
    {
        [Test]
        public void UxmlInstantiatesControlsAndAppliesAuthoredAttributes()
        {
            var template = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(
                "Assets/Frontier/Tests/EditMode/Game/FrontierControlsFixture.uxml");
            Assert.IsNotNull(template);
            var root = template.CloneTree();
            var button = root.Q<FrontierButton>("action");
            Assert.IsNotNull(button);
            Assert.IsTrue(button.Primary);
            Assert.IsTrue(button.Large);
            Assert.AreEqual("COLLECT", button.text);
            Assert.IsTrue(root.Q<FrontierCard>("card").Highlighted);
            Assert.AreEqual(0.65f, root.Q<FrontierProgress>("progress").Value, 0.001f);
            var chip = root.Q<FrontierChip>("cost");
            Assert.AreEqual("3/10", chip.Text);
            Assert.AreEqual("wood", chip.Icon);
            Assert.IsTrue(chip.Low);
            Assert.AreEqual("TOOLS", root.Q<FrontierTab>("tab").Caption);
            Assert.IsTrue(root.Q<FrontierTab>("tab").Selected);
            Assert.IsTrue(root.Q<FrontierTabBar>("tabs").ClassListContains("fr-tabs"));
            Assert.AreEqual("100", root.Q<FrontierStatRow>("stat").Value);
            Assert.IsTrue(root.Q<FrontierModal>("modal").Wide);
            Assert.IsTrue(root.Q<FrontierPopup>("popup").Compact);
            Assert.AreEqual("A nested row label", root.Q<FrontierListRow>("row").Q<Label>().text);
            Assert.AreEqual("READY", root.Q<FrontierBadge>("badge").text);
            Assert.AreEqual("Backpack full!", root.Q<FrontierToast>("toast").Text);
        }

        [Test]
        public void HelpersUseSharedControlsAndPreserveClassesAndContent()
        {
            var button = UiKit.Btn("BUILD", UiKit.Lime, null, true, true);
            Assert.IsInstanceOf<FrontierButton>(button);
            Assert.IsTrue(button.ClassListContains("fr-btn-big"));
            Assert.IsTrue(button.ClassListContains("fr-btn-primary"));
            var bar = UiKit.Bar(UiKit.Cyan, out var fill);
            Assert.IsInstanceOf<FrontierProgress>(bar);
            Assert.AreSame(bar[0], fill);
            UiKit.SetFill(fill, 0.8f);
            Assert.AreEqual(80f, fill.style.width.value.value, 0.001f);
            Assert.AreEqual(0.8f, ((FrontierProgress)bar).Value, 0.001f);
            var chip = UiKit.Chip("wood", UiKit.Lime, "12", true);
            Assert.IsInstanceOf<FrontierChip>(chip);
            Assert.AreEqual("12", chip.Q<Label>().text);
            Assert.AreEqual(2, chip.childCount);
        }

        [Test]
        public void RebindingControlsDoesNotAccumulateChildrenOrStaleState()
        {
            var chip = new FrontierChip { Icon = "wood", Low = true };
            chip.Icon = "stone";
            chip.Tint = UiKit.Amber;
            chip.Low = false;
            Assert.AreEqual(2, chip.childCount);
            Assert.IsFalse(chip.ClassListContains("fr-itemchip-low"));
            Assert.AreEqual(StyleKeyword.Null, chip.Q<Label>().style.color.keyword);
            chip.Icon = "";
            Assert.AreEqual(1, chip.childCount);
            var tab = new FrontierTab { Icon = "research", Selected = true };
            tab.Selected = false;
            tab.Accent = UiKit.Magenta;
            Assert.AreEqual(2, tab.childCount);
            Assert.IsFalse(tab.ClassListContains("fr-tab-on"));
            var card = new FrontierCard { Accent = UiKit.Magenta, Highlighted = true };
            card.Highlighted = false;
            Assert.IsFalse(card.ClassListContains("fr-card-on"));
            Assert.IsFalse(card.ClassListContains(UiKit.AccentClass(UiKit.Magenta)));
            var progress = new FrontierProgress { Value = float.NaN };
            Assert.AreEqual(0f, progress.Value);
            progress.Value = 5f;
            Assert.AreEqual(1f, progress.Value);
            progress.Value = -5f;
            Assert.AreEqual(0f, progress.Value);
        }
    }
}
