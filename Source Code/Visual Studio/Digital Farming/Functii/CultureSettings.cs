using System.Collections.Generic;

namespace Digital_Farming.Functii
{
    public static class CultureSettings
    {
        private static readonly Dictionary<string, (float phMin, float phMax,
                                                     float ecMin, float ecMax,
                                                     int tdsMin, int tdsMax,                                                    
                                                     float wTempMin, float wTempMax,
                                                     float aTempMin, float aTempMax,
                                                     float hMin, float hMax)> _map
        = new()
        {
            // Format: (pHmin, pHmax, ECmin, ECmax, TDSmin, TDSmax, wTmin, wTmax, aTmin, aTmax, hMin, hMax)
            ["Tomatoes"] = (5.5f, 6.5f, 1.2f, 2.0f, 700, 1200, 20f, 24f, 18f, 26f, 60f, 80f),
            ["Cucumber"] = (5.5f, 6.0f, 1.3f, 2.2f, 800, 1500, 22f, 26f, 20f, 28f, 70f, 90f),
            ["Cabbage"] = (6.0f, 7.0f, 1.5f, 2.0f, 1000, 1500, 18f, 22f, 16f, 24f, 60f, 80f),
            ["Lettuce"] = (5.5f, 6.5f, 1.0f, 1.5f, 600, 900, 18f, 22f, 16f, 24f, 50f, 70f),
            ["Basil"] = (5.5f, 6.5f, 1.2f, 1.8f, 750, 1250, 20f, 26f, 18f, 26f, 60f, 80f),
            ["Spinach"] = (6.5f, 7.5f, 1.2f, 1.8f, 750, 1250, 15f, 20f, 15f, 22f, 50f, 70f),
            ["Peppers"] = (5.5f, 6.5f, 1.5f, 2.3f, 750, 1250, 20f, 26f, 18f, 28f, 60f, 80f),
            ["Strawberry"] = (5.5f, 6.5f, 1.2f, 1.8f, 750, 1000, 18f, 22f, 16f, 24f, 65f, 85f),
            ["Microgreens"] = (6.0f, 7.0f, 0.6f, 1.0f, 300, 600, 18f, 20f, 16f, 24f, 50f, 70f),
        };

        public static void ApplyToProfile(Profil p)
        {
            if (_map.TryGetValue(p.Culture, out var r))
            {
                p.PHMin = r.phMin;
                p.PHMax = r.phMax;
                p.ECMin = r.ecMin;      
                p.ECMax = r.ecMax;      
                p.TDSMin = r.tdsMin;
                p.TDSMax = r.tdsMax;
                p.WaterTempMin = r.wTempMin;
                p.WaterTempMax = r.wTempMax;
                p.AmbientTempMin = r.aTempMin;
                p.AmbientTempMax = r.aTempMax;
                p.HumidityMin = r.hMin;
                p.HumidityMax = r.hMax;
            }
            else
            {
                p.PHMin = 0; p.PHMax = 14;
                p.ECMin = 0; p.ECMax = 5;      
                p.TDSMin = 0; p.TDSMax = 2500;
                p.WaterTempMin = 0; p.WaterTempMax = 40;
                p.AmbientTempMin = 0; p.AmbientTempMax = 40;
                p.HumidityMin = 0; p.HumidityMax = 100;
            }
        }

    }
}
