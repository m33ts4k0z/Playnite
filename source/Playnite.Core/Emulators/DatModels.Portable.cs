#if !WINDOWS
using System;
using YamlDotNet.Serialization;

namespace Playnite.Emulators
{
    public class DatGame
    {
        public int Id { get; set; }

        [DatProperty("name")]
        public string Name { get; set; }

        [DatProperty("region")]
        public string Region { get; set; }

        [DatProperty("releaseyear")]
        public string ReleaseYear { get; set; }

        [DatProperty("serial")]
        public string Serial { get; set; }

        [DatProperty("rom.crc")]
        public string RomCrc { get; set; }

        [DatProperty("rom.name")]
        public string RomName { get; set; }

        [YamlIgnore]
        [DatProperty("rom.serial")]
        public string RomSerial { get; set; }

        [YamlIgnore]
        [DatProperty("origin")]
        public string Origin { get; set; }

        [YamlIgnore]
        [DatProperty("comment")]
        public string Comment { get; set; }

        [YamlIgnore]
        public string SanitizedName => Name.IsNullOrEmpty() ? string.Empty : Emulators.RomName.SanitizeName(Name);

        public override string ToString()
        {
            return $"{Serial} {RomCrc} {Name}";
        }

        public void CopyTo(DatGame target)
        {
            if (!Name.IsNullOrEmpty() && target.Name.IsNullOrEmpty())
            {
                target.Name = Name;
            }

            if (!Origin.IsNullOrEmpty() && Region.IsNullOrEmpty() && target.Region.IsNullOrEmpty())
            {
                target.Region = Origin;
            }

            if (!Region.IsNullOrEmpty() && target.Region.IsNullOrEmpty())
            {
                target.Region = Region;
            }

            if (!ReleaseYear.IsNullOrEmpty() && target.ReleaseYear.IsNullOrEmpty())
            {
                target.ReleaseYear = ReleaseYear;
            }

            if (!RomSerial.IsNullOrEmpty() && target.Serial.IsNullOrEmpty())
            {
                target.Serial = RomSerial;
            }

            if (!Serial.IsNullOrEmpty() && target.Serial.IsNullOrEmpty())
            {
                target.Serial = Serial;
            }
        }

        public void FixData()
        {
            if (Serial.IsNullOrEmpty() && !RomSerial.IsNullOrEmpty())
            {
                Serial = RomSerial;
            }

            if (Region.IsNullOrEmpty() && !Origin.IsNullOrEmpty())
            {
                Region = Origin;
            }

            if (Name.IsNullOrEmpty() && !Comment.IsNullOrEmpty())
            {
                Name = Comment;
            }
        }
    }

    public class DatPropertyAttribute : Attribute
    {
        public string Name { get; }

        public DatPropertyAttribute(string name)
        {
            Name = name;
        }
    }
}
#endif
