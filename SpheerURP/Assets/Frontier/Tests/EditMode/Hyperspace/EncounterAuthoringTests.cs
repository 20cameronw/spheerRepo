using System;
using NUnit.Framework;

namespace Frontier.Hyperspace.Tests
{
    public sealed class EncounterAuthoringTests
    {
        static EncounterTemplate Template()
        {
            return new TemplateBuilder("authored", 0, 12, 1f, EncounterRole.Calm, EncounterFamily.Drones)
                .Drone(0f, 0f, 0f).Drone(1f, 0f, 30f).Done();
        }

        [Test]
        public void ImportedLibraryOwnsItsTemplatesAndAssignsStableIndices()
        {
            var first = Template();
            var second = Template();
            second.id = "second";
            var library = EncounterLibrary.FromTemplates(new[] { first, second });
            first.entries[0].x = 99f;
            first.cost = 999f;
            Assert.AreEqual(0f, library[0].entries[0].x);
            Assert.AreEqual(1f, library[0].cost);
            Assert.AreEqual(0, library[0].index);
            Assert.AreEqual(1, library[1].index);
            Assert.AreSame(library[1], library.ById("second"));
        }

        [Test]
        public void EveryDefaultTemplatePassesAuthoringValidation()
        {
            var defaults = EncounterLibrary.CreateDefault();
            var templates = new EncounterTemplate[defaults.Count];
            for (int i = 0; i < templates.Length; i++) templates[i] = defaults[i];
            var imported = EncounterLibrary.FromTemplates(templates);
            Assert.AreEqual(defaults.Count, imported.Count);
            for (int i = 0; i < imported.Count; i++)
            {
                Assert.AreEqual(defaults[i].id, imported[i].id);
                Assert.AreEqual(defaults[i].entries, imported[i].entries);
            }
        }

        [Test]
        public void InvalidLibrariesAreRejectedInsteadOfSilentlyFallingBack()
        {
            Assert.Throws<ArgumentNullException>(() => EncounterLibrary.FromTemplates(null));
            Assert.Throws<ArgumentException>(() => EncounterLibrary.FromTemplates(Array.Empty<EncounterTemplate>()));
            Assert.Throws<ArgumentException>(() => EncounterLibrary.FromTemplates(new EncounterTemplate[] { null }));
            var template = Template();
            Assert.Throws<ArgumentException>(() => EncounterLibrary.FromTemplates(new[] { template, template }));
            template.cost = float.NaN;
            Assert.Throws<ArgumentException>(() => EncounterLibrary.FromTemplates(new[] { template }));
            template.cost = 1f;
            template.entries[1].zOffset = -1f;
            Assert.Throws<ArgumentException>(() => EncounterLibrary.FromTemplates(new[] { template }));
            template.entries[1].zOffset = 30f;
            template.durationZ = 20f;
            Assert.Throws<ArgumentException>(() => EncounterLibrary.FromTemplates(new[] { template }));
        }
    }
}
