using System;
using System.Threading.Tasks;

using CitizenFX.Core;

namespace TrackM.Client
{
    internal class TrackedEntity
    {
        public int UpdateInterval { get; }

        public int MovementThreshold { get; }

        public Entity Entity { get; }

        public Vector3 LastPosition { get; private set; }

        public string Type { get; }

        private readonly Client client;

        public TrackedEntity(Client client, int handle, int updateInterval, int movementThreshold)
        {
            this.client = client;

            Entity = Entity.FromHandle(handle);
            if (Entity == null)
            {
                throw new ArgumentException("invalid entity handle");
            }

            UpdateInterval = updateInterval;
            MovementThreshold = movementThreshold;

            switch (Entity)
            {
                case Vehicle _:
                    // TODO: maybe identify vehicle types? ex. plane, boat, police, fire, etc.
                    Type = "vehicle";
                    break;
                case Ped _:
                    Type = "ped";
                    break;
                default:
                    Type = "unknown";
                    break;
            }
        }

        public async Task Tick()
        {
            await BaseScript.Delay(UpdateInterval);

            if (Entity == null || !Entity.IsAlive)
            {
                client.Untrack(Entity.Handle);
                return;
            }

            Vector3 p = Entity.Position;

            bool moved = p.DistanceToSquared(LastPosition) >= MovementThreshold;
            if (!moved)
            {
                return;
            }

            LastPosition = p;

            BaseScript.TriggerServerEvent(Client.Prefix + "MetadataSet", client.Player.ServerId, Entity.Handle, "pos", string.Join(",", p.X, p.Y));
        }
    }
}
