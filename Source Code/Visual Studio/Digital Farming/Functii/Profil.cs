using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Digital_Farming.Functii
{
    public class Profil
    {
        // Profile Settings

        public string Culture { get; set; }
        public int PlantCount { get; set; }
        public float ContainerSizeL { get; set; }
        public string SubstrateType { get; set; }

        // Current Values
        public float EC { get; set; }
        public float PH { get; set; }
        public int TDS { get; set; }
        public float WaterTempC { get; set; }
        public float AmbientTempC { get; set; }
        public float HumidityPct { get; set; }

        public float UptakeRatePerPlantPerDay { get; set; }


        // Minimum and maximum values for each parameter

        public float PHMin { get; set; }
        public float PHMax { get; set; }

        public float ECMin { get; set; }
        public float ECMax { get; set; }


        public int TDSMin { get; set; }
        public int TDSMax { get; set; }

        public float WaterTempMin { get; set; }
        public float WaterTempMax { get; set; }

        public float AmbientTempMin { get; set; }
        public float AmbientTempMax { get; set; }

        public float HumidityMin { get; set; }
        public float HumidityMax { get; set; }


    }
}
