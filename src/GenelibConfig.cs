using System;
using Vintagestory.API.Common;

namespace Genelib {
    public class GenelibConfig {
        public static GenelibConfig Instance = null!;

        public static double MutationRate = 0.00004;
        public static double EggIncubationTime = 1;

        public float InbreedingResistance = 0.6f;

        public int ConfigVersion = 0;

        public void MakeValid() {
            InbreedingResistance = Math.Clamp(InbreedingResistance, 0.05f, 0.9f);
        }

        public static void Load(ICoreAPI api) {
            try {
                Instance = api.LoadModConfig<GenelibConfig>("genelib.json");
            }
            catch (Exception e) {
                api.Logger.Error("Failed to load config file for Genelib: " + e);
            }
            if (Instance == null) {
                Instance = new GenelibConfig();
            }
            Instance.MakeValid();
            api.StoreModConfig(Instance, "genelib.json");
        }
    }
}
