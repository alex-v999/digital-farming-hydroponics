using System;
using System.Collections.Generic;

namespace Digital_Farming.Functii
{
    public class NotificationService
    {
        private readonly Profil _profile;

        public NotificationService(Profil profile)
        {
            _profile = profile;
        }

        public IEnumerable<string> Evaluate(float uptakeRatePerPlantPerDay)
        {
            var messages = new List<string>();

            // 1) pH out of range?
            if (_profile.PH < _profile.PHMin)
                messages.Add(
                  $"⚠ pH too low ({_profile.PH:0.##}); " +
                  $"ideal is {_profile.PHMin:0.##}–{_profile.PHMax:0.##}."
                );
            else if (_profile.PH > _profile.PHMax)
                messages.Add(
                  $"⚠ pH too high ({_profile.PH:0.##}); " +
                  $"ideal is {_profile.PHMin:0.##}–{_profile.PHMax:0.##}."
                );

            // 2) TDS out of range?
            if (_profile.TDS < _profile.TDSMin)
                messages.Add(
                  $"⚠ TDS too low ({_profile.TDS}); " +
                  $"ideal is {_profile.TDSMin}–{_profile.TDSMax} ppm."
                );
            else if (_profile.TDS > _profile.TDSMax)
                messages.Add(
                  $"⚠ TDS too high ({_profile.TDS}); " +
                  $"ideal is {_profile.TDSMin}–{_profile.TDSMax} ppm."
                );

            // 3) Temperature
            if (_profile.WaterTempC < _profile.WaterTempMin || _profile.WaterTempC > _profile.WaterTempMax)
                messages.Add(
                  $"⚠ Water temp out of range ({_profile.WaterTempC:0.##}°C); " +
                  $"ideal is {_profile.WaterTempMin:0.##}–{_profile.WaterTempMax:0.##}°C."
                );
            if (_profile.AmbientTempC < _profile.AmbientTempMin || _profile.AmbientTempC > _profile.AmbientTempMax)
                messages.Add(
                  $"⚠ Ambient temp out of range ({_profile.AmbientTempC:0.##}°C); " +
                  $"ideal is {_profile.AmbientTempMin:0.##}–{_profile.AmbientTempMax:0.##}°C."
                );

            // 4) Humidity
            if (_profile.HumidityPct < _profile.HumidityMin || _profile.HumidityPct > _profile.HumidityMax)
                messages.Add(
                  $"⚠ Humidity out of range ({_profile.HumidityPct:0.##}%); " +
                  $"ideal is {_profile.HumidityMin:0.##}–{_profile.HumidityMax:0.##}%."
                );

            // 5) Nutrient days remaining
            // Total grams in tank:
            //   target ppm = midpoint of TDSMin/TDSMax
            int targetPpm = (_profile.TDSMin + _profile.TDSMax) / 2;
            float totalGrams = targetPpm * _profile.ContainerSizeL / 1000f;

            // Daily uptake:
            float dailyUptake = _profile.UptakeRatePerPlantPerDay * _profile.PlantCount;

            float daysLeft = dailyUptake > 0
                ? totalGrams / dailyUptake
                : float.PositiveInfinity;

            if (daysLeft < 1)
                messages.Add($"ℹ Nutrient solution will run out in under a day (~{daysLeft:0.0} days).");
            else if (daysLeft < 3)
                messages.Add($"ℹ Nutrient solution will run out in ~{daysLeft:0.0} days. Plan to refill soon.");
            else
                messages.Add($"✔ Nutrient solution has ~{daysLeft:0.0} days remaining.");

            return messages;
        }
    }
}
