# GeoTrack Telemetry Pipeline

## Payload Contract
GpsClient now emits compact telemetry messages containing only the following fields:

```json
{
  "id": "BUGGY-001",
  "datetime": "2025-10-16T14:28:05Z",
  "lat": 10.7626227,
  "lng": 106.6601725,
  "sats": 12
}
```

GeoTrack ingests the same payload and computes the device status (Stationary, Moving, Idle) based on a sliding time window. The ingest response contains the current status:

```json
{
  "id": "BUGGY-001",
  "status": "Stationary"
}
```

Legacy fields (`speedKph`, `headingDeg`, `batteryPct`, `status`, `deviceId`, `timestamp`) are no longer serialized anywhere in the stack.

## Device State Classification
`GeoTrack` maintains a window of recent samples for every device and applies hysteresis when switching states:

| Threshold | Default | Description |
|-----------|---------|-------------|
| `windowSeconds` | `10` | Size of the rolling window used for metrics. |
| `maxDistanceStationaryMeters` | `10` | Maximum pairwise distance in the window for a device to be considered stationary. |
| `speedThresholdStationaryMps` | `0.5` | Average speed ceiling for the stationary state. |
| `speedThresholdMovingMps` | `1.0` | Average speed floor for the moving state. |
| `confirmCount` | `2` | Number of consecutive candidate readings required before committing to a new status. |
| `outlierJumpMeters` | `200` | Speed threshold used to drop unrealistic jumps (per second). |
| `minPoints` | `3` | Minimum samples required before evaluating a status. |

The values can be tuned in `GeoTrack/devices.config.json` under the `tracking` section.

## Observability
Status transitions and dropped outliers are logged with the source `Tracking`. Window sizes are logged for every ingest to help diagnose tuning issues.

## Tests
Unit tests cover serialization on the client and the server-side tracking rules (stationary/moving/idle transitions, hysteresis, outlier handling, and minimum sample requirements).
