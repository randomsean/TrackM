using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;

using CitizenFX.Core;

namespace TrackM.Client
{
    public class Client : BaseScript
    {
        public const string Prefix = "tm:";

        public Player Player => LocalPlayer;

        private readonly ConcurrentDictionary<int, TrackedEntity> entities = new ConcurrentDictionary<int, TrackedEntity>();

        public Client()
        {
            EventHandlers[Prefix + "Track"] += new Action<int, int, int>(Track);
            EventHandlers[Prefix + "Untrack"] += new Action<int>(Untrack);
        }
        
        private void Track(int handle, int updateInterval, int movementThreshold)
        {
            if (entities.ContainsKey(handle))
            {
                return;
            }

            var e = new TrackedEntity(this, handle, updateInterval, movementThreshold);

            entities[handle] = e;

            TriggerServerEvent(Prefix + "register", handle, $"Entity #{handle} ({Player.Name})", e.Type);

            Tick += e.Tick;
        }

        public void Untrack(int handle)
        {
            bool removed = entities.TryRemove(handle, out TrackedEntity e);
            if (removed)
            {
                Tick -= e.Tick;
                TriggerServerEvent(Prefix + "unregister", handle);
            }
        }
    }
}
