using Mutagen.Bethesda;
using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Plugins.Cache;
using Mutagen.Bethesda.Plugins.Order;
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

        private void ProcessSummoningSpellBook(IBookGetter bookGetter, ISpellGetter aimedSpellGetter)
        {
            Console.WriteLine("Processing summoning spell book " + bookGetter);

            // bookGetter.EditorID?.Equals("SpellTomeConjureFrostAtronach");

            GetScriptObjectProperty(GetScriptEntry(patchMod.Books.GetOrAddAsOverride(bookGetter))).Object.SetTo(MakeSelfTargetedSpell(aimedSpellGetter, FindExistingSpell(bookGetter)));
        }

        private ISpellGetter? FindExistingSpell(IBookGetter bookGetter)
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

        private static ScriptEntry GetScriptEntry(Book book)
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

        private static IScriptObjectProperty GetScriptObjectProperty(ScriptEntry scriptEntry)
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

        private ISpellGetter MakeSelfTargetedSpell(ISpellGetter aimedSpellGetter, ISpellGetter? foundSelfTargetedSpell)
        {
            if (aimedToSelfSpellGetter.TryGetValue(aimedSpellGetter, out var selfTargetedSpellGetter)) return selfTargetedSpellGetter;

            var selfTargetedSpell = foundSelfTargetedSpell is not null
                ? patchMod.Spells.GetOrAddAsOverride(foundSelfTargetedSpell)
                : patchMod.Spells.AddNew(aimedSpellGetter.EditorID + "_Self");

            selfTargetedSpell.DeepCopyIn(aimedSpellGetter, out var errorMask2, copyMask: SpellCopyMask);
            if (errorMask2.Overall is not null) throw errorMask2.Overall;

            selfTargetedSpell.Range = 0;
            selfTargetedSpell.TargetType = TargetType.Self;

            if (aimedSpellGetter.Name?.String is not null)
                (selfTargetedSpell.Name ??= new(aimedSpellGetter.Name.TargetLanguage)).String = settings.Value.SpellPrefix + aimedSpellGetter.Name.String + settings.Value.SpellSuffix;

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

            aimedToSelfSpellGetter.Add(aimedSpellGetter, selfTargetedSpell);

            return selfTargetedSpell;
        }

        private readonly Dictionary<IMagicEffectGetter, IMagicEffectGetter> aimedToSelfMagicEffectGetter = new();

        private static readonly MagicEffect.TranslationMask MagicEffectCopyMask = new(true)
        {
            EditorID = false,
            Name = false,
            Description = false,
            TargetType = false,
        };

        private IMagicEffectGetter MakeSelfTargetedMagicEffect(IMagicEffectGetter aimedMagicEffectGetter, IMagicEffectGetter? foundSelfTargetedMagicEffect = null)
        {
            if (aimedToSelfMagicEffectGetter.TryGetValue(aimedMagicEffectGetter, out var selfTargetedMagicEffectGetter)) return selfTargetedMagicEffectGetter;

            var selfTargetedMagicEffect = foundSelfTargetedMagicEffect is not null
                ? patchMod.MagicEffects.GetOrAddAsOverride(foundSelfTargetedMagicEffect)
                : patchMod.MagicEffects.AddNew(aimedMagicEffectGetter.EditorID + "_Self");

            selfTargetedMagicEffect.DeepCopyIn(aimedMagicEffectGetter, out var errorMask, copyMask: MagicEffectCopyMask);
            if (errorMask.Overall is not null) throw errorMask.Overall;
            selfTargetedMagicEffect.TargetType = TargetType.Self;
            if (aimedMagicEffectGetter.Name?.String is not null)
                (selfTargetedMagicEffect.Name ??= new(aimedMagicEffectGetter.Name.TargetLanguage)).String = settings.Value.SpellPrefix + aimedMagicEffectGetter.Name.String + settings.Value.SpellSuffix;
            if (aimedMagicEffectGetter.Description?.String is not null)
                (selfTargetedMagicEffect.Description ??= new(aimedMagicEffectGetter.Description.TargetLanguage)).String = aimedMagicEffectGetter.Description.String.Replace(settings.Value.WordsToReplaceInDescription, settings.Value.ReplacmentWordsForDescription);

            aimedToSelfMagicEffectGetter.Add(aimedMagicEffectGetter, selfTargetedMagicEffect);

            return selfTargetedMagicEffect;
        }
    }
}
