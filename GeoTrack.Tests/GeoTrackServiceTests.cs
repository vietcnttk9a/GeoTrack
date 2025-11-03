using System;
using System.Collections.Generic;
using GeoTrack.Config;
using GeoTrack.Domain;
using Xunit;

namespace GeoTrack.Tests;

public class GeoTrackServiceTests
{
    private static GeoTrackService CreateService(
        TrackingOptions? options = null,
        List<string>? infoLog = null,
        List<string>? warningLog = null)
    {
        options ??= new TrackingOptions();
        var store = new InMemoryDeviceWindowStore();
        return new GeoTrackService(
            store,
            options,
            infoLog == null ? null : infoLog.Add,
            warningLog == null ? null : warningLog.Add);
    }

    [Fact]
    public void StationarySequenceProducesStationary()
    {
        var options = new TrackingOptions
        {
            ConfirmCount = 2,
            MinPoints = 3
        };
        var service = CreateService(options);
        var baseTime = DateTime.UtcNow;

        var status = DeviceStatus.Unknown;
        for (var i = 0; i < 5; i++)
        {
            status = service.OnPosition(
                "dev-1",
                new Position
                {
                    Lat = 10.0 + (i * 0.000001),
                    Lng = 20.0,
                    Timestamp = baseTime.AddSeconds(i * 3)
                });
        }

        Assert.Equal(DeviceStatus.Stationary, status);
    }

    [Fact]
    public void MovingSequenceProducesMoving()
    {
        var options = new TrackingOptions
        {
            ConfirmCount = 2,
            MinPoints = 3
        };
        var service = CreateService(options);
        var baseTime = DateTime.UtcNow;

        var status = DeviceStatus.Unknown;
        for (var i = 0; i < 5; i++)
        {
            status = service.OnPosition(
                "dev-2",
                new Position
                {
                    Lat = 10.0 + (i * 0.00002),
                    Lng = 20.0,
                    Timestamp = baseTime.AddSeconds(i)
                });
        }

        Assert.Equal(DeviceStatus.Moving, status);
    }

    [Fact]
    public void IdleSequenceProducesIdle()
    {
        var options = new TrackingOptions
        {
            ConfirmCount = 2,
            MinPoints = 3,
            SpeedThresholdStationaryMps = 0.5,
            SpeedThresholdMovingMps = 1.0
        };
        var service = CreateService(options);
        var baseTime = DateTime.UtcNow;

        var status = DeviceStatus.Unknown;
        for (var i = 0; i < 5; i++)
        {
            status = service.OnPosition(
                "dev-3",
                new Position
                {
                    Lat = 10.0 + (i * 0.000005),
                    Lng = 20.0,
                    Timestamp = baseTime.AddSeconds(i * 2)
                });
        }

        Assert.Equal(DeviceStatus.Idle, status);
    }

    [Fact]
    public void HysteresisRequiresMultipleConfirmations()
    {
        var options = new TrackingOptions
        {
            ConfirmCount = 2,
            MinPoints = 3
        };
        var service = CreateService(options);
        var baseTime = DateTime.UtcNow;

        DeviceStatus status = DeviceStatus.Unknown;
        for (var i = 0; i < 4; i++)
        {
            status = service.OnPosition(
                "dev-4",
                new Position
                {
                    Lat = 10.0 + (i * 0.000001),
                    Lng = 20.0,
                    Timestamp = baseTime.AddSeconds(i * 3)
                });
        }
        Assert.Equal(DeviceStatus.Stationary, status);

        status = service.OnPosition(
            "dev-4",
            new Position
            {
                Lat = 10.0 + 0.001,
                Lng = 20.0,
                Timestamp = baseTime.AddSeconds(20)
            });
        Assert.Equal(DeviceStatus.Stationary, status);

        status = service.OnPosition(
            "dev-4",
            new Position
            {
                Lat = 10.0 + 0.002,
                Lng = 20.0,
                Timestamp = baseTime.AddSeconds(21)
            });
        Assert.Equal(DeviceStatus.Moving, status);
    }

    [Fact]
    public void OutlierIsIgnored()
    {
        var options = new TrackingOptions
        {
            ConfirmCount = 2,
            MinPoints = 3,
            OutlierJumpMeters = 50
        };
        var warnings = new List<string>();
        var service = CreateService(options, warningLog: warnings);
        var baseTime = DateTime.UtcNow;

        DeviceStatus status = DeviceStatus.Unknown;
        for (var i = 0; i < 4; i++)
        {
            status = service.OnPosition(
                "dev-5",
                new Position
                {
                    Lat = 10.0,
                    Lng = 20.0 + (i * 0.000001),
                    Timestamp = baseTime.AddSeconds(i * 3)
                });
        }
        Assert.Equal(DeviceStatus.Stationary, status);

        status = service.OnPosition(
            "dev-5",
            new Position
            {
                Lat = 11.0,
                Lng = 25.0,
                Timestamp = baseTime.AddSeconds(10)
            });

        Assert.Equal(DeviceStatus.Stationary, status);
        Assert.NotEmpty(warnings);
    }

    [Fact]
    public void InsufficientPointsKeepsCurrentStatus()
    {
        var options = new TrackingOptions
        {
            ConfirmCount = 2,
            MinPoints = 5
        };
        var service = CreateService(options);
        var baseTime = DateTime.UtcNow;

        var status = DeviceStatus.Unknown;
        for (var i = 0; i < 3; i++)
        {
            status = service.OnPosition(
                "dev-6",
                new Position
                {
                    Lat = 10.0,
                    Lng = 20.0,
                    Timestamp = baseTime.AddSeconds(i * 2)
                });
        }

        Assert.Equal(DeviceStatus.Unknown, status);
    }
}
