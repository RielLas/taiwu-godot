using System.Collections.Generic;
using Godot;

namespace Taiwu_Godot.Scripts.UI
{
    /// <summary>
    /// Ledger artwork, loaded on first use and shared by all four tab controls.
    /// Tries a direct file read first so the art still shows up when the import cache is cold.
    /// </summary>
    internal static class LedgerArt
    {
        private static readonly Dictionary<string, Texture2D?> Cache = new();

        public static Texture2D? FrameBarBronze => Get("UI/frame_bar_bronze.png");
        public static Texture2D? FillEmerald => Get("UI/fill_fluid_emerald.png");
        public static Texture2D? FillCyan => Get("UI/fill_fluid_cyan.png");
        public static Texture2D? FillCrimson => Get("UI/fill_fluid_crimson.png");
        public static Texture2D? SlotIron => Get("UI/frame_slot_iron.png");
        public static Texture2D? SlotBronze => Get("UI/frame_slot_bronze.png");
        public static Texture2D? SlotBrass => Get("UI/frame_slot_brass.png");
        public static Texture2D? SlotJade => Get("UI/frame_slot_jade.png");
        public static Texture2D? BaguaMandala => Get("Tokens/dao_bagua_mandala.png");

        public static Texture2D? DaoHuman => Get("DaoPaths/dao_human_seal.png");
        public static Texture2D? DaoMoon => Get("DaoPaths/dao_moon_seal.png");
        public static Texture2D? DaoStrength => Get("DaoPaths/dao_strength_seal.png");
        public static Texture2D? DaoBone => Get("DaoPaths/dao_bone_seal.png");
        public static Texture2D? DaoPoison => Get("DaoPaths/dao_poison_seal.png");
        public static Texture2D? DaoBlood => Get("DaoPaths/dao_blood_seal.png");
        public static Texture2D? DaoRefine => Get("DaoPaths/dao_refine_seal.png");
        public static Texture2D? DaoEnslave => Get("DaoPaths/dao_enslave_seal.png");

        public static Texture2D? BannerGuYue => Get("ClanBanners/banner_guyue_clan.png");
        public static Texture2D? BannerBai => Get("ClanBanners/banner_bai_clan.png");
        public static Texture2D? BannerXiong => Get("ClanBanners/banner_xiong_clan.png");

        public static Texture2D? IconMoonShadow => Get("Combat/gu_moon_shadow.png");
        public static Texture2D? IconBoarStrength => Get("Combat/gu_boar_strength.png");
        public static Texture2D? IconMoonglow => Get("Combat/gu_moonglow.png");

        private static Texture2D? Get(string relativePath)
        {
            if (Cache.TryGetValue(relativePath, out var cached)) return cached;

            var texture = Load($"res://Resources/{relativePath}");
            Cache[relativePath] = texture;
            return texture;
        }

        private static Texture2D? Load(string path)
        {
            try
            {
                string globalPath = ProjectSettings.GlobalizePath(path);
                if (System.IO.File.Exists(globalPath))
                {
                    var img = Image.LoadFromFile(globalPath);
                    if (img != null && !img.IsEmpty())
                    {
                        return ImageTexture.CreateFromImage(img);
                    }
                }
            }
            catch { }

            if (ResourceLoader.Exists(path))
            {
                try
                {
                    return GD.Load<Texture2D>(path);
                }
                catch { }
            }
            return null;
        }
    }
}
