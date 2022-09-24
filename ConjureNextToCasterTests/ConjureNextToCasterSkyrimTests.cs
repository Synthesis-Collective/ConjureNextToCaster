using ConjureNextToCaster;
using FsCheck;
using FsCheck.Xunit;
using Mutagen.Bethesda;
using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Plugins.Order;
using Mutagen.Bethesda.Plugins.Records;
using Mutagen.Bethesda.Skyrim;
using Xunit;
using Xunit.Abstractions;

namespace ConjureNextToCasterTests
{
    public class ConjureNextToCasterSkyrimTests
    {
        public static readonly ModKey masterModKey = ModKey.FromNameAndExtension("Master.esm");

        public static readonly ModKey patchModKey = ModKey.FromNameAndExtension("Patch.esp");

        private readonly SkyrimMod masterMod;

        private readonly SkyrimMod patchMod;

        private readonly LoadOrder<IModListing<ISkyrimModGetter>> loadOrder;

        private readonly ITestOutputHelper _TestOutputHelper;

        public ConjureNextToCasterSkyrimTests(ITestOutputHelper testOutputHelper)
        {
            _TestOutputHelper = testOutputHelper;

            masterMod = new SkyrimMod(masterModKey, SkyrimRelease.SkyrimSE);
            patchMod = new SkyrimMod(patchModKey, SkyrimRelease.SkyrimSE);

            loadOrder = new LoadOrder<IModListing<ISkyrimModGetter>>
            {
                new ModListing<ISkyrimModGetter>(masterMod, true),
                new ModListing<ISkyrimModGetter>(patchMod, true)
            };
        }

        [Fact]
        public void TestDoesNothing()
        {
            var patcher = new ConjureNextToCasterSkyrim(loadOrder, patchMod, loadOrder.ToImmutableLinkCache(), new(() => new Settings()));

            patcher.Run();

            Assert.Empty(patchMod.EnumerateMajorRecords());
        }

        [Theory]
        [InlineData(true, true, true)]
        [InlineData(true, true, false)]
        [InlineData(true, false, false)]
        [InlineData(false, false, false)]
        public void RunTest(bool hasSpell, bool isSummoningSpell, bool hasExistingSpell)
        {
            var book = masterMod.Books.AddNew("Book");

            if (hasSpell)
            {
                var teaches = new BookSpell();
                book.Teaches = teaches;

                Spell spell = masterMod.Spells.AddNew("SummoningSpell");
                teaches.Spell.SetTo(spell);

                spell.TargetType = TargetType.TargetLocation;

                var effect = masterMod.MagicEffects.AddNew("SummoningEffect");
                spell.Effects.Add(new() { BaseEffect = effect.ToNullableLink() });

                effect.CastType = CastType.FireAndForget;
                effect.TargetType = TargetType.TargetLocation;

                if (isSummoningSpell)
                {
                    effect.Archetype = new MagicEffectSummonCreatureArchetype();

                    if (hasExistingSpell)
                    {
                        var existingEntry = masterMod.Spells.AddNew("ModifiedSummoningSpell");

                        book.VirtualMachineAdapter = new();

                        ConjureNextToCasterSkyrim.GetScriptObjectProperty(ConjureNextToCasterSkyrim.GetScriptEntry(book)).Object.SetTo(existingEntry);
                    }
                }
            }

            var patcher = new ConjureNextToCasterSkyrim(loadOrder, patchMod, loadOrder.ToImmutableLinkCache(), new(() => new Settings()));

            patcher.Run();

            if (hasSpell && isSummoningSpell)
                Assert.NotEmpty(patchMod.EnumerateMajorRecords());
            else
                Assert.Empty(patchMod.EnumerateMajorRecords());
        }

        [Theory]
        [InlineData(true, true)]
        [InlineData(true, false)]
        [InlineData(false, false)]
        public void FindExistingSpellTest(bool hasSpell, bool isSummoningSpell)
        {
            var book = masterMod.Books.AddNew("Book");

            Spell? existingEntry = null;

            if (hasSpell)
            {
                existingEntry = masterMod.Spells.AddNew("SummoningSpell");

                var effect = masterMod.MagicEffects.AddNew("SummoningEffect");

                existingEntry.Effects.Add(new() { BaseEffect = effect.ToNullableLink() });

                effect.CastType = CastType.FireAndForget;
                if (isSummoningSpell)
                    effect.Archetype = new MagicEffectSummonCreatureArchetype();

                book.VirtualMachineAdapter = new();

                ConjureNextToCasterSkyrim.GetScriptObjectProperty(ConjureNextToCasterSkyrim.GetScriptEntry(book)).Object.SetTo(existingEntry);
            }

            var patcher = new ConjureNextToCasterSkyrim(loadOrder, patchMod, loadOrder.ToImmutableLinkCache(), new(() => new Settings()));

            var foundEntry = patcher.FindExistingSpell(book);

            if (hasSpell && isSummoningSpell)
                Assert.Same(existingEntry, foundEntry);
            else
                Assert.Null(foundEntry);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void GetScriptEntryTest(bool exists)
        {
            var book = masterMod.Books.AddNew("Book");

            ScriptEntry? existingEntry = null;

            if (exists)
            {
                existingEntry = new()
                {
                    Name = "DankAddSecondSpell"
                };

                book.VirtualMachineAdapter = new();

                book.VirtualMachineAdapter.Scripts.Add(existingEntry);
            }

            var foundEntry = ConjureNextToCasterSkyrim.GetScriptEntry(book);

            if (existingEntry is not null)
                Assert.Same(existingEntry, foundEntry);

            Assert.Equal("DankAddSecondSpell", foundEntry.Name);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void GetScriptObjectPropertyTest(bool exists)
        {
            var scriptEntry = new ScriptEntry();

            IScriptObjectProperty? existingProperty = null;

            if (exists)
            {
                existingProperty = new ScriptObjectProperty
                {
                    Name = "SecondSpell",
                    Flags = ScriptProperty.Flag.Edited
                };
                scriptEntry.Properties.Add((ScriptProperty)existingProperty);
            }

            var foundProperty = ConjureNextToCasterSkyrim.GetScriptObjectProperty(scriptEntry);

            if (existingProperty is not null)
                Assert.Same(existingProperty, foundProperty);

            Assert.Equal("SecondSpell", foundProperty.Name);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void MakeSelfTargetedSpellTest(bool hasExistingRecord)
        {
            var settings = new Settings();

            var spell = masterMod.Spells.AddNew("Original_Spell");

            spell.TargetType = TargetType.Aimed;
            spell.Range = 42;

            var patcher = new ConjureNextToCasterSkyrim(loadOrder, patchMod, loadOrder.ToImmutableLinkCache(), new(() => settings));

            ISpell? existingSpell = null;

            if (hasExistingRecord)
            {
                existingSpell = patchMod.Spells.AddNew("Modified_Spell");
                existingSpell.TargetType = TargetType.Aimed;
                existingSpell.Range = 32;
            }

            var result = patcher.MakeSelfTargetedSpell(spell, existingSpell);

            Assert.Equal(TargetType.Self, result.TargetType);
            Assert.Equal(0, result.Range);

            var result2 = patcher.MakeSelfTargetedSpell(spell, existingSpell);

            Assert.Same(result, result2);
        }

        [Theory]
        [InlineData(true, true, true)]
        [InlineData(false, true, true)]
        [InlineData(true, false, true)]
        [InlineData(false, false, true)]
        [InlineData(true, true, false)]
        [InlineData(false, true, false)]
        [InlineData(true, false, false)]
        [InlineData(false, false, false)]
        public void EditMagicEffectsTest(bool hasExistingRecord, bool hasOtherEffects, bool hasMoreEffects)
        {
            Dictionary<string, INpcGetter> npcs = new();

            INpcGetter AddNpc(string name)
            {
                if (npcs.TryGetValue(name, out var target))
                    return target;
                target = masterMod.Npcs.AddNew(name + "_NPC");
                npcs.Add(name, target);
                return target;
            }

            void AddOtherEffects(ISkyrimMod mod, ISpell victim)
            {
                if (!hasOtherEffects) return;

                var effect = mod.MagicEffects.AddNew("OtherEffect");
                effect.CastType = CastType.Concentration;

                victim.Effects.Add(new()
                {
                    BaseEffect = effect.ToNullableLink(),
                });
            }

            void AddEffect(ISkyrimMod mod, ISpell victim, string name, string npcName)
            {
                var target = AddNpc(npcName);

                var arch = new MagicEffectSummonCreatureArchetype();
                arch.Association.SetTo(target);
                arch.AssociationKey = target.FormKey; // why?
                Assert.Equal(target.FormKey, arch.Association.FormKey);

                var baseEffect = mod.MagicEffects.AddNew(name);
                baseEffect.CastType = CastType.FireAndForget;
                baseEffect.Archetype = arch;

                Effect effect = new();
                effect.BaseEffect.SetTo(baseEffect);

                victim.Effects.Add(effect);
            }

            ISpell originalSpell = masterMod.Spells.AddNew("Original_Spell");
            AddEffect(masterMod, originalSpell, "SummoningEffect", "NPC1");
            if (hasMoreEffects)
                AddEffect(masterMod, originalSpell, "SummoningEffect2", "NPC2");
            AddOtherEffects(masterMod, originalSpell);

            Spell? existingSpell = null;

            if (hasExistingRecord)
            {
                existingSpell = masterMod.Spells.AddNew("Original_Modified_Spell");
                AddOtherEffects(masterMod, existingSpell);
                AddEffect(masterMod, existingSpell, "Original_ModifiedSummoningEffect", "NPC1");
                if (hasMoreEffects)
                    AddEffect(masterMod, existingSpell, "Original_ModifiedSummoningEffect2", "NPC2");
            }

            var patcher = new ConjureNextToCasterSkyrim(loadOrder, patchMod, loadOrder.ToImmutableLinkCache(), new(() => new Settings()));

            ISpell newSpell = existingSpell is not null
                ? patchMod.Spells.GetOrAddAsOverride(existingSpell)
                : patchMod.Spells.AddNew("Modified_Spell");

            newSpell.DeepCopyIn(originalSpell, new Spell.TranslationMask(true) { EditorID = false });

            patcher.EditMagicEffects(newSpell, existingSpell);

            var linkCache = loadOrder.ToImmutableLinkCache();

            // TODO I guess we've proven it works?

            var victimToMagicEffect = new Dictionary<IFormLinkGetter<INpcGetter>, IMagicEffectGetter>();

            foreach (var effect in newSpell.Effects)
            {
                if (!effect.BaseEffect.TryResolve(linkCache, out var magicEffect)) continue;
                if (magicEffect.CastType != CastType.FireAndForget) continue;
                if (magicEffect.Archetype is not IMagicEffectSummonCreatureArchetypeGetter foo) continue;

                Assert.Equal(TargetType.Self, magicEffect.TargetType);

                if (hasExistingRecord)
                    Assert.StartsWith("Original_Modified", magicEffect.EditorID);

                victimToMagicEffect.Add(foo.Association, magicEffect);
            }

            Assert.Equal(npcs.Count, victimToMagicEffect.Count);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void MakeSelfTargetedMagicEffectTest(bool hasExistingRecord)
        {
            var settings = new Settings();

            var magicEffect = masterMod.MagicEffects.AddNew("Original_Effect");

            magicEffect.TargetType = TargetType.Aimed;

            var patcher = new ConjureNextToCasterSkyrim(loadOrder, patchMod, loadOrder.ToImmutableLinkCache(), new(() => settings));

            IMagicEffect? existingMagicEffect = null;

            if (hasExistingRecord)
            {
                existingMagicEffect = patchMod.MagicEffects.AddNew("Modified_Effect");
                existingMagicEffect.TargetType = TargetType.Aimed;
            }

            var result = patcher.MakeSelfTargetedMagicEffect(magicEffect, existingMagicEffect);

            Assert.Equal(TargetType.Self, result.TargetType);

            var result2 = patcher.MakeSelfTargetedMagicEffect(magicEffect, existingMagicEffect);

            Assert.Same(result, result2);
        }

        [Property]
        public void EditNameTest(string startingName, string spellPrefix, string spellName, string spellSuffix)
        {
            var settings = new Settings();

            if (spellPrefix is not null) settings.SpellPrefix = spellPrefix;
            if (spellSuffix is not null) settings.SpellSuffix = spellSuffix;

            var magicEffect = masterMod.MagicEffects.AddNew("Original_Effect");

            if (spellName is not null) magicEffect.Name = spellName;

            var patcher = new ConjureNextToCasterSkyrim(loadOrder, patchMod, loadOrder.ToImmutableLinkCache(), new(() => settings));

            var result = patchMod.MagicEffects.AddNew("Modified_Effect");

            if (startingName is not null) result.Name = startingName;

            patcher.EditName(magicEffect, result);

            if (spellName is not null)
                Assert.Equal(settings.SpellPrefix + spellName + settings.SpellSuffix, result.Name!.String);
            else
                Assert.Null(result.Name);
        }

        [Property]
        public void EditDescriptionTest(string startingDescription, NonEmptyString descriptionPrefix, NonNull<string> description, NonNull<string> descriptionReplacement, NonEmptyString descriptionSuffix)
        {
            Action value = () =>
                {
                    var settings = new Settings
                    {
                        WordsToReplaceInDescription = description.Get,
                        ReplacmentWordsForDescription = descriptionReplacement.Get
                    };

                    var magicEffect = masterMod.MagicEffects.AddNew("Original_Effect");

                    magicEffect.Description = descriptionPrefix.Get + settings.WordsToReplaceInDescription + descriptionSuffix.Get;

                    var patcher = new ConjureNextToCasterSkyrim(loadOrder, patchMod, loadOrder.ToImmutableLinkCache(), new(() => settings));

                    var result = patchMod.MagicEffects.AddNew("Modified_Effect");

                    if (startingDescription is not null) result.Description = startingDescription;

                    patcher.EditDescription(magicEffect, result);

                    Assert.Equal(descriptionPrefix + settings.ReplacmentWordsForDescription + descriptionSuffix, result.Description?.String);
                };
            value
                .When(!(descriptionPrefix.Get.Contains(description.Get) || descriptionSuffix.Get.Contains(description.Get)));
        }
    }
}