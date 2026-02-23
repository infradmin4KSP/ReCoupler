namespace ReCoupler
{
    internal static class ReCouplerSettings
    {
        public const float connectRadius_default = 0.1f;
        public const float connectAngle_default = 91;
        public const bool allowRoboJoints_default = false;
        public const bool allowKASJoints_default = false;
        public const string configURL = "ReCoupler/ReCouplerSettings/ReCouplerSettings";

        public static float connectRadius = connectRadius_default;
        public static float connectAngle = connectAngle_default;
        public static bool allowRoboJoints = allowRoboJoints_default;
        public static bool allowKASJoints = allowKASJoints_default;

        public static bool showGUI = true;
        public static bool isCLSInstalled = false;
        public static bool settingsLoaded = false;

        public static void LoadSettings()
        {
            if (settingsLoaded)
                return;

            var cfgs = GameDatabase.Instance.GetConfigs("ReCouplerSettings");
            if (cfgs.Length > 0)
            {
                for (int i = 0; i < cfgs.Length; i++)
                {
                    if (cfgs[i].url.Equals(configURL))
                    {
                        if (float.TryParse(cfgs[i].config.GetValue("connectRadius"), out float loadedRadius))
                            connectRadius = loadedRadius;

                        if (float.TryParse(cfgs[i].config.GetValue("connectAngle"), out float loadedAngle))
                            connectAngle = loadedAngle;

                        if (bool.TryParse(cfgs[i].config.GetValue("allowRoboJoints"), out bool loadedAllowRoboJoints))
                            allowRoboJoints = loadedAllowRoboJoints;

                        if (bool.TryParse(cfgs[i].config.GetValue("allowKASJoints"), out bool loadedAllowKASJoints))
                            allowKASJoints = loadedAllowKASJoints;

                        if (!bool.TryParse(cfgs[i].config.GetValue("showGUI"), out bool loadedShowGUI))
                            showGUI = true;
                        else
                            showGUI = loadedShowGUI;
                        break;
                    }
                    else if (i == cfgs.Length - 1)
                    {
                        UnityEngine.Debug.LogWarning("ReCouplerSettings: Couldn't find the correct settings file. Using default values.");
                    }
                }
            }
            else
            {
                UnityEngine.Debug.LogWarning("ReCouplerSettings: Couldn't find the settings file. Using default values.");
            }

            settingsLoaded = true;
        }
    }
}
