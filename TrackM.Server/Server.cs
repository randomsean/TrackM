using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;

using CitizenFX.Core;
using CitizenFX.Core.Native;

using Sider;

namespace TrackM.Server
{
    internal class Server : BaseScript
    {
        public const string Prefix = "tm:";

        private const int MinimumUpdateInterval = 500;
        private const int MinimumMovementThreshold = 1;

        private readonly ThreadwisePool pool;
        private readonly int updateInterval;
        private readonly int movementThreshold;

        // reservedKeys cannot be deleted from entity metadata.
        private readonly string[] reservedKeys = {"name", "icon", "pos"};

        public Server()
        {
            string redisAddr = API.GetConvar("trackm_redis_addr", "127.0.0.1:6379");
            SplitHostPort(redisAddr, out string redisHost, out int redisPort);

            int redisDb = API.GetConvarInt("trackm_redis_db", 0);

            var settings = RedisSettings.Build()
                .Host(redisHost)
                .Port(redisPort)
                .ReconnectOnIdle(true)
                .ReissueCommandsOnReconnect(false);

            pool = new ThreadwisePool(settings, redisDb);

            IRedisClient<string> client = pool.GetClient();
            client.FlushDb();

            updateInterval = API.GetConvarInt("trackm_update_interval", 1000);
            if (updateInterval < MinimumUpdateInterval)
            {
                Debug.WriteLine("trackm_update_interval set too low ({0}ms) using minimum {1}ms.", updateInterval, MinimumUpdateInterval);
                updateInterval = MinimumUpdateInterval;
            }

            movementThreshold = API.GetConvarInt("trackm_movement_threshold", 1);
            if (movementThreshold < MinimumMovementThreshold)
            {
                Debug.WriteLine("trackm_movement_threshold set too low ({0}m) using minimum {1}m.", movementThreshold, MinimumMovementThreshold);
                movementThreshold = MinimumMovementThreshold;
            }
            // clients deal with squared distances
            movementThreshold *= movementThreshold; 
            
            // fivem events
            EventHandlers["playerDropped"] += new Action<Player, string>(PlayerDropped);

            // internal events
            EventHandlers[Prefix + "register"] += new Action<Player, int, string, string>(Register);
            EventHandlers[Prefix + "unregister"] += new Action<Player, int>(Unregister);

            // public events
            EventHandlers[Prefix + "Track"] += new Action<int, int>(Track);
            EventHandlers[Prefix + "Untrack"] += new Action<int, int>(Untrack);
            EventHandlers[Prefix + "MetadataGet"] += new Action<int, int, string, CallbackDelegate>(MetadataGet);
            EventHandlers[Prefix + "MetadataSet"] += new Action<int, int, string, string>(MetadataSet);
            EventHandlers[Prefix + "MetadataDelete"] += new Action<int, int, string>(MetadataDelete);
        }

        private void PlayerDropped([FromSource] Player player, string reason)
        {
            IRedisClient<string> client = pool.GetClient();

            string[] handles = client.SMembers($"entities_{player.Handle}");

            client.Pipeline(c =>
            {
                foreach (string handle in handles)
                {
                    client.SRem("entities", $"{player.Handle}_{handle}");
                    client.Del($"meta_{player.Handle}_{handle}");
                }
            });

            client.Del($"entities_{player.Handle}");
        }

        /// <summary>
        /// Event handler for requesting tracking of an entity.
        /// </summary>
        /// <param name="player">Player's network ID</param>
        /// <param name="handle">Entity's handle</param>
        private void Track(int player, int handle)
        {
            Player p = Players[player];
            if (p == null)
            {
                return;
            }

            p.TriggerEvent(Prefix + "Track", handle, updateInterval, movementThreshold);
        }

        /// <summary>
        /// Event handler for untracking an entity.
        /// </summary>
        /// <param name="player">Player's network ID</param>
        /// <param name="handle">Entity's handle</param>
        private void Untrack(int player, int handle)
        {
            Player p = Players[player];
            if (p == null)
            {
                return;
            }

            p.TriggerEvent(Prefix + "Untrack", handle);
        }

        /// <summary>
        /// Event handler for getting metadata field values.
        /// </summary>
        /// <param name="player">Player's network ID</param>
        /// <param name="handle">Entity's handle</param>
        /// <param name="key">field key</param>
        /// <param name="result">value result callback</param>
        private void MetadataGet(int player, int handle, string key, CallbackDelegate result)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                return;
            }

            IRedisClient<string> client = pool.GetClient();

            Player p = Players[player];
            if (p == null)
            {
                return;
            }

            string value = client.HGet($"meta_{player}_{handle}", key);
            result?.Invoke(value);
        }

        /// <summary>
        /// Event handler for setting metadata field values.
        /// </summary>
        /// <param name="player">Player's network ID</param>
        /// <param name="handle">Entity's handle</param>
        /// <param name="key">field key</param>
        /// <param name="value">value to set</param>
        private void MetadataSet(int player, int handle, string key, string value)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                return;
            }

            IRedisClient<string> client = pool.GetClient();

            Player p = Players[player];
            if (p == null)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(value))
            {
                // delete field if set to null value
                client.HDel($"meta_{player}_{handle}", key);
                return;
            }

            bool exists = client.Exists($"meta_{player}_{handle}");
            if (!exists)
            {
                // not tracked, so don't create a new hash
                return;
            }

            client.HSet($"meta_{player}_{handle}", key, value);
        }

        /// <summary>
        /// Event handler for deleting metadata fields.
        /// </summary>
        /// <param name="player">Player's network ID</param>
        /// <param name="handle">Entity's handle</param>
        /// <param name="key">field key</param>
        private void MetadataDelete(int player, int handle, string key)
        {
            foreach (string reserved in reservedKeys)
            {
                if (key == reserved)
                {
                    return;
                }
            }

            // null value will delete the field
            MetadataSet(player, handle, key, null);
        }

        /// <summary>
        /// Register is an event sent from a client to let the server know an entity is being tracked.
        /// </summary>
        /// <param name="player">the player</param>
        /// <param name="handle">the entity's handle</param>
        /// <param name="name">the entitiy's display name</param>
        /// <param name="type">the entity type</param>
        private void Register([FromSource] Player player, int handle, string name, string type)
        {
            IRedisClient<string> client = pool.GetClient();

            client.SAdd($"entities", $"{player.Handle}_{handle}");

            client.SAdd($"entities_{player.Handle}", handle.ToString());

            client.HSet($"meta_{player.Handle}_{handle}", "name", name);
            client.HSet($"meta_{player.Handle}_{handle}", "pos", "0,0");
            client.HSet($"meta_{player.Handle}_{handle}", "icon", type);
        }

        /// <summary>
        /// Unregister is an event sent from a client to let the server cleanup a tracked entity.
        /// </summary>
        /// <param name="player">the player</param>
        /// <param name="handle">the entity's handle</param>
        private void Unregister([FromSource] Player player, int handle)
        {
            IRedisClient<string> client = pool.GetClient();

            client.Del($"meta_{player.Handle}_{handle}");
            client.SRem($"entities_{player.Handle}", handle.ToString());
            client.SRem($"entities", $"{player.Handle}_{handle}");
        }

        private static void SplitHostPort(string input, out string host, out int port)
        {
            input = input.Trim();

            int colon = input.LastIndexOf(':');
            if (colon == -1)
                throw new ArgumentException("Missing port number");

            if (input[0] == '[')
            {
                // IPv6
                int end = input.IndexOf(']');
                if (end == -1)
                    throw new ArgumentException("Missing ']'");

                host = input.Substring(1, end);
            }
            else
            {
                host = input.Substring(0, colon);
            }

            port = int.Parse(input.Substring(colon+1));
        }
    }
}
