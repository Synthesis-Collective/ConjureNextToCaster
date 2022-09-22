namespace ConjureNextToCaster
{
    public record Settings
    {
        public string SpellPrefix = "";

        public string SpellSuffix = " (Next to Caster)";

        public string WordsToReplaceInDescription = "wherever the caster is pointing";

        public string ReplacmentWordsForDescription = "right next to the caster";
    }
}