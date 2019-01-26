<h1 align="center">
    TrackM
    <small>&mdash; Live Map for FiveM</small>
</h1>
TrackM is a entity tracking live map for FiveM game servers.

**NOTE: There are two parts to a TrackM server: the FiveM script and the web interface. This repository only contains the FiveM script.**

## Install

### Requirements
* A FiveM server
* Redis 2.8+ server

### From a Release Build
* From the [releases]() page download the latest zip or tar.gz file.
* Unzip or untar the file to your server `releases` directory.
* Add `start trackm` to you `server.cfg` file.
* See [Configuration](#Configuration) for further set up.

## Configuration

### Convars
The following settings can be set in the `server.cfg` file:
| Name                        | Type     | Default          | Comments                                                                                         |
| --------------------------- | -------- | ---------------- | ------------------------------------------------------------------------------------------------ |
| `trackm_redis_addr`         | `string` | `127.0.0.1:6379` | Redis server hostname and port                                                                   |
| `trackm_redis_db`           | `int`    | `0`              | Redis database index See Redis [SELECT](https://redis.io/commands/select) command for more info. |
| `trackm_update_interval`    | `int`    | `1000`           | The frequency the client checks for position updates (milliseconds). Minimum value is 500.       |
| `trackm_movement_threshold` | `int`    | `1`              | The distance an entity must move before their position is updated (meters). Minimum value is 1.  |

#### Example `server.cfg`
```
start trackm

set trackm_redis_addr "127.0.0.1:6379"
set trackm_redis_db 7
set trackm_update_interval 2000
set trackm_movement_threshold 2
```

## Developers

TrackM can be controlled from other scripts running on the same FiveM server.

## Events
The following events can be sent to the server to change TrackM data:

| Name                | Parameters                                                   | Description                                                             |
| ------------------- | ------------------------------------------------------------ | ----------------------------------------------------------------------- |
| `tm:Track`          | `int player, int handle`                                     | Starts tracking an Entity if not already tracked.                       |
| `tm:Untrack`        | `int player, int handle`                                     | Stops tracking an Entity if they are being tracked.                     |
| `tm:MetadataGet`    | `int player, int handle, string key, callback(string value)` | Gets the value associated with the specified key on the Entity.         |
| `tm:MetadataSet`    | `int player, int handle, string key, string value`           | Adds or sets the value associated with the specified key on the Entity. |
| `tm:MetadataDelete` | `int player, int handle, string key`                         | Deletes the value with the specified key on the Entity.                 |

#### Example Usage (C#)
```
Player player = ...
Entity entity = ...

// Starts tracking the Entity with the handle 5678 and owned by player "1234".
TriggerServerEvent("tm:Track", player.ServerId, entity.Handle);

// Changes an Entity's name on the web interface
TriggerServerEvent("tm:MetadataSet", player.ServerId, entity.Handle, "name", "Police Unit #6");

// Changes an Entity's icon on the web interface
TriggerServerEvent("tm:MetadataSet", player.ServerId, entity.Handle, "icon", "police");
```

### Metadata
The following metadata keys have special functionality on the web interface and can be changed by external scripts:

| Key    | Type     | Supported Values                        |
| ------ | -------- | --------------------------------------- |
| `name` | `string` | Any valid UTF-8 string                  |
| `icon` | `string` | `vehicle`, `ped`, `unknown`             |
| `pos`  | `string` | A position vector in the form `[x],[y]` |
**NOTE: These keys cannot be deleted; only set.**

## Dependencies
TrackM uses the following open source libraries:
| Dependency                                | License      |
| ----------------------------------------- | ------------ |
| [Sider](https://github.com/chakrit/sider) | BSD-3-Clause |