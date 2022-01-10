﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace RandomizerMod.Settings
{
    [Serializable]
    public class LongLocationSettings : SettingsModule
    {
        public enum WPSetting
        {
            Allowed,
            ExcludePathOfPain,
            ExcludeWhitePalace
        }

        public enum BossEssenceSetting
        {
            All,
            ExcludeGreyPrinceZoteAndWhiteDefender,
            ExcludeAllDreamBosses,
            ExcludeAllDreamWarriors
        }

        public enum CostItemHintSettings
        {
            CostAndName,
            CostOnly,
            NameOnly,
            None
        }

        public WPSetting RandomizationInWhitePalace;
        public BossEssenceSetting BossEssenceRandomization;

        public bool ColosseumPreview;
        public bool KingFragmentPreview;

        public bool FlowerQuestPreview;
        public bool GreyPrinceZotePreview;

        public bool WhisperingRootPreview;
        public bool DreamerPreview;

        public bool AbyssShriekPreview;
        public bool VoidHeartPreview;

        public bool GodtunerPreview;
        public bool LoreTabletPreview;

        public bool BasinFountainPreview;
        public bool NailmasterPreview;

        public bool StagPreview;
        public bool MapPreview;
        
        public bool DivinePreview;

        public CostItemHintSettings GeoShopPreview;
        public CostItemHintSettings GrubfatherPreview;
        public CostItemHintSettings SeerPreview;
        public CostItemHintSettings EggShopPreview;
    }
}
