using Mutagen.Bethesda;
using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Plugins.Aspects;
using Mutagen.Bethesda.Plugins.Cache;
using Mutagen.Bethesda.Plugins.Order;
using Mutagen.Bethesda.Plugins.Records;
using Mutagen.Bethesda.Skyrim;
using Noggog;

namespace ConjureNextToCaster
{
    public class ConjureNextToCasterSkyrim
    {
        private readonly ILoadOrder<IModListing<ISkyrimModGetter>> loadOrder;

        private readonly ISkyrimMod patchMod;

        private readonly ILinkCache<ISkyrimMod, ISkyrimModGetter> linkCache;

        private readonly Lazy<Settings> settings;

        public ConjureNextToCasterSkyrim(ILoadOrder<IModListing<ISkyrimModGetter>> LoadOrder, ISkyrimMod PatchMod, ILinkCache<ISkyrimMod, ISkyrimModGetter> LinkCache, Lazy<Settings> Settings)
        {
            loadOrder = LoadOrder;
            patchMod = PatchMod;
            linkCache = LinkCache;
            settings = Settings;
        }

        internal void Run()
        {
            foreach (var bookGetter in loadOrder.PriorityOrder.Book().WinningOverrides())
            {
                if (bookGetter.Teaches is not IBookSpellGetter spellBookGetter) continue;

                if (!spellBookGetter.Spell.TryResolve(linkCache, out var aimedSpellGetter)) continue;

                if (aimedSpellGetter.TargetType != TargetType.TargetLocation) continue;

                foreach (var effectGetter in aimedSpellGetter.Effects)
                {
                    if (!effectGetter.BaseEffect.TryResolve(linkCache, out var aimedMagicEffectGetter)) continue;
                    if (aimedMagicEffectGetter.CastType != CastType.FireAndForget) continue;
                    if (aimedMagicEffectGetter.Archetype is not IMagicEffectSummonCreatureArchetypeGetter) continue;
                    if (aimedMagicEffectGetter.TargetType != TargetType.TargetLocation) continue;
                    ProcessSummoningSpellBook(bookGetter, aimedSpellGetter);
                    break;
                }
            }
        }

        internal void ProcessSummoningSpellBook(IBookGetter bookGetter, ISpellGetter aimedSpellGetter)
        {
            Console.WriteLine("Processing summoning spell book " + bookGetter);

            GetScriptObjectProperty(GetScriptEntry(patchMod.Books.GetOrAddAsOverride(bookGetter))).Object.SetTo(MakeSelfTargetedSpell(aimedSpellGetter, FindExistingSpell(bookGetter)));
        }

        internal ISpellGetter? FindExistingSpell(IBookGetter bookGetter)
        {
            if (bookGetter.VirtualMachineAdapter is null) return null;

            foreach (var scriptEntry in bookGetter.VirtualMachineAdapter.Scripts)
            {
                if (!scriptEntry.Name.Equals("DankAddSecondSpell")) continue;

                foreach (var scriptProperty in scriptEntry.Properties)
                {
                    if (scriptProperty is not IScriptObjectPropertyGetter scriptObjectProperty1) continue;
                    if (!scriptProperty.Name.Equals("SecondSpell")) continue;
                    if (!scriptObjectProperty1.Object.TryResolve<ISpellGetter>(linkCache, out var spellGetter)) continue;

                    foreach (var effectGetter in spellGetter.Effects)
                    {
                        if (!effectGetter.BaseEffect.TryResolve(linkCache, out var magicEffectGetter)) continue;
                        if (magicEffectGetter.CastType != CastType.FireAndForget) continue;
                        if (magicEffectGetter.Archetype is not IMagicEffectSummonCreatureArchetypeGetter) continue;

                        return spellGetter;
                    }
                }
            }
            return null;
        }

        internal static ScriptEntry GetScriptEntry(Book book)
        {
            book.VirtualMachineAdapter ??= new();

            foreach (var candidateScriptEntry in book.VirtualMachineAdapter.Scripts)
                if (candidateScriptEntry.Name.Equals("DankAddSecondSpell"))
                    return candidateScriptEntry;

            ScriptEntry scriptEntry = new()
            {
                Name = "DankAddSecondSpell"
            };

            book.VirtualMachineAdapter.Scripts.Add(scriptEntry);

            return scriptEntry;
        }

        internal static IScriptObjectProperty GetScriptObjectProperty(ScriptEntry scriptEntry)
        {
            foreach (var scriptProperty in scriptEntry.Properties)
                if (scriptProperty.Name.Equals("SecondSpell") && scriptProperty is IScriptObjectProperty candidateScriptObjectProperty)
                    return candidateScriptObjectProperty;

            IScriptObjectProperty? scriptObjectProperty = new ScriptObjectProperty
            {
                Name = "SecondSpell",
                Flags = ScriptProperty.Flag.Edited
            };
            scriptEntry.Properties.Add((ScriptProperty)scriptObjectProperty);

            return scriptObjectProperty;
        }

        private readonly Dictionary<ISpellGetter, ISpellGetter> aimedToSelfSpellGetter = new();

        private static readonly Spell.TranslationMask SpellCopyMask = new(true)
        {
            EditorID = false,
            Name = false,
            Description = false,
            TargetType = false,
            Range = false,
        };

        internal ISpellGetter MakeSelfTargetedSpell(ISpellGetter aimedSpellGetter, ISpellGetter? foundSelfTargetedSpell)
        {
            if (aimedToSelfSpellGetter.TryGetValue(aimedSpellGetter, out var selfTargetedSpellGetter)) return selfTargetedSpellGetter;

            var selfTargetedSpell = foundSelfTargetedSpell is not null
                ? patchMod.Spells.GetOrAddAsOverride(foundSelfTargetedSpell)
                : patchMod.Spells.AddNew(aimedSpellGetter.EditorID + "_Self");

            selfTargetedSpell.DeepCopyIn(aimedSpellGetter, copyMask: SpellCopyMask);

            selfTargetedSpell.Range = 0;
            selfTargetedSpell.TargetType = TargetType.Self;

            EditName(aimedSpellGetter, selfTargetedSpell);

            EditMagicEffects(selfTargetedSpell, foundSelfTargetedSpell);

            aimedToSelfSpellGetter.Add(aimedSpellGetter, selfTargetedSpell);

            return selfTargetedSpell;
        }

        internal void EditMagicEffects(ISpell selfTargetedSpell, ISpellGetter? foundSelfTargetedSpell)
        {
            Dictionary<IFormLinkGetter<INpcGetter>, IMagicEffectGetter>? victimToMagicEffect = null;

            if (foundSelfTargetedSpell is not null)
            {
                victimToMagicEffect = new();

                foreach (var effect in foundSelfTargetedSpell.Effects)
                {
                    if (!effect.BaseEffect.TryResolve(linkCache, out var aimedMagicEffect)) continue;
                    if (aimedMagicEffect.CastType != CastType.FireAndForget) continue;
                    if (aimedMagicEffect.Archetype is not IMagicEffectSummonCreatureArchetypeGetter foo) continue;

                    victimToMagicEffect.Add(foo.Association, aimedMagicEffect);
                }
            }

            foreach (var effect in selfTargetedSpell.Effects)
            {
                if (!effect.BaseEffect.TryResolve(linkCache, out var aimedMagicEffect)) continue;
                if (aimedMagicEffect.CastType != CastType.FireAndForget) continue;
                if (aimedMagicEffect.Archetype is not IMagicEffectSummonCreatureArchetypeGetter foo) continue;

                if (foundSelfTargetedSpell is not null)
                {
                    victimToMagicEffect!.TryGetValue(foo.Association, out var foundSelfTargetedMagicEffect);
                    effect.BaseEffect.SetTo(MakeSelfTargetedMagicEffect(aimedMagicEffect, foundSelfTargetedMagicEffect));
                }
                else
                    effect.BaseEffect.SetTo(MakeSelfTargetedMagicEffect(aimedMagicEffect));
            }
        }

        private readonly Dictionary<IMagicEffectGetter, IMagicEffectGetter> aimedToSelfMagicEffectGetter = new();

        private static readonly MagicEffect.TranslationMask MagicEffectCopyMask = new(true)
        {
            EditorID = false,
            Name = false,
            Description = false,
            TargetType = false,
        };

        internal IMagicEffectGetter MakeSelfTargetedMagicEffect(IMagicEffectGetter aimedMagicEffectGetter, IMagicEffectGetter? foundSelfTargetedMagicEffect = null)
        {
            if (aimedToSelfMagicEffectGetter.TryGetValue(aimedMagicEffectGetter, out var selfTargetedMagicEffectGetter)) return selfTargetedMagicEffectGetter;

            var selfTargetedMagicEffect = foundSelfTargetedMagicEffect is not null
                ? patchMod.MagicEffects.GetOrAddAsOverride(foundSelfTargetedMagicEffect)
                : patchMod.MagicEffects.AddNew(aimedMagicEffectGetter.EditorID + "_Self");

            selfTargetedMagicEffect.DeepCopyIn(aimedMagicEffectGetter, copyMask: MagicEffectCopyMask);

            selfTargetedMagicEffect.TargetType = TargetType.Self;
            EditName(aimedMagicEffectGetter, selfTargetedMagicEffect);
            EditDescription(aimedMagicEffectGetter, selfTargetedMagicEffect);

            aimedToSelfMagicEffectGetter.Add(aimedMagicEffectGetter, selfTargetedMagicEffect);

            return selfTargetedMagicEffect;
        }

        internal void EditDescription(IMagicEffectGetter aimedMagicEffectGetter, IMagicEffect selfTargetedMagicEffect)
        {
            if (aimedMagicEffectGetter.Description?.String is not null)
            {
                if (settings.Value.WordsToReplaceInDescription.Length > 0)
                    (selfTargetedMagicEffect.Description ??= new(aimedMagicEffectGetter.Description.TargetLanguage)).String = aimedMagicEffectGetter.Description.String.Replace(settings.Value.WordsToReplaceInDescription, settings.Value.ReplacmentWordsForDescription);
            }
            else
                selfTargetedMagicEffect.Description = null;
        }

        internal void EditName(ITranslatedNamedGetter translatedNamedGetter, ITranslatedNamed translatedNamed)
        {
            if (translatedNamedGetter.Name?.String is not null)
                (translatedNamed.Name ??= new(translatedNamedGetter.Name.TargetLanguage)).String = settings.Value.SpellPrefix + translatedNamedGetter.Name.String + settings.Value.SpellSuffix;
            else
                translatedNamed.Name = null;
        }
    }
}
