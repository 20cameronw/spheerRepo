using System;
using UnityEngine;

namespace Frontier
{
    /// <summary>
    /// The save codec on top of Unity's JsonUtility: the only place that knows the save is JSON. Core owns the schema (plain POCOs) and
    /// the file protocol; this class is just text in, objects out. JsonUtility ignores fields it does not know (a save from a newer
    /// build still parses) and leaves fields that are missing from the file at their defaults.
    /// </summary>
    public sealed class SaveSerializer : ISaveCodec
    {
        public static readonly SaveSerializer Instance = new SaveSerializer();

        [Serializable]
        sealed class SchemaProbe
        {
            public int schemaVersion;
        }

        public string Serialize(SaveV2 save)
        {
            if (save == null) throw new ArgumentNullException("save");
            return JsonUtility.ToJson(save);
        }

        public SaveV2 DeserializeV2(string text)
        {
            var save = JsonUtility.FromJson<SaveV2>(text);
            if (save == null) throw new FormatException("the text is not a save");
            return save;
        }

        public SaveDataV1 DeserializeV1(string text)
        {
            var save = JsonUtility.FromJson<SaveDataV1>(text);
            if (save == null) throw new FormatException("the text is not a v1 save");
            return save;
        }

        public int ReadSchemaVersion(string text)
        {
            var probe = JsonUtility.FromJson<SchemaProbe>(text);
            if (probe == null) throw new FormatException("the text is not a save");
            return probe.schemaVersion;
        }
    }
}
