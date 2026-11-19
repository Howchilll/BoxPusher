namespace HowEnum
{
    public static class UINameEnum
    {
        public class Tag { }

        public static readonly EnumKey<Tag> TitlePanel = new("TitlePanel");
        public static readonly EnumKey<Tag> LevelSelectPanel = new("LevelSelectPanel");
        public static readonly EnumKey<Tag> PlayPanel = new("PlayPanel");
        public static readonly EnumKey<Tag> WinPanel = new("WinPanel");
        public static readonly EnumKey<Tag> SettingsPanel = new("SettingsPanel");
        public static readonly EnumKey<Tag> EditorPanel = new("EditorPanel");
        public static readonly EnumKey<Tag> AlertPanel = new("AlertPanel");
    }
}
