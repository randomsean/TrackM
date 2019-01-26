using System;
using System.Threading.Tasks;

using CitizenFX.Core;

namespace TrackM.Demo
{
    public class Script : BaseScript
    {
        private Vehicle Vehicle { get; set; }

        private bool? lastSirenActive;

        public Script()
        {
            Tick += CheckControls;
        }

        private Task CheckControls()
        {
            if (Game.IsControlJustPressed(0, Control.DropWeapon))
            {
                if (LocalPlayer.Character.CurrentVehicle != null && Vehicle != LocalPlayer.Character.CurrentVehicle)
                {
                    Vehicle = LocalPlayer.Character.CurrentVehicle;
                    TriggerServerEvent("tm:Track", LocalPlayer.ServerId, Vehicle.Handle);
                    Tick += UpdateVehicle;
                }
            }

            return Task.FromResult(0);
        }

        private async Task UpdateVehicle()
        {
            await Delay(1000);

            if (Vehicle == null || !Vehicle.IsAlive)
            {
                Tick -= UpdateVehicle;
                return;
            }

            bool sirenActive = Vehicle.IsSirenActive;
            // only send deltas
            if (lastSirenActive == null || sirenActive != lastSirenActive)
            {
                TriggerServerEvent("tm:MetadataSet", LocalPlayer.ServerId, Vehicle.Handle, "Lightbar", sirenActive ? "On" : "Off");
            }
            
            TriggerServerEvent("tm:MetadataSet", LocalPlayer.ServerId, Vehicle.Handle, "Speed", $"{Math.Round(Vehicle.Speed * 2.2369)} mph");

            lastSirenActive = sirenActive;
        }
    }
}
