﻿using Maestro.Core.Dtos;
using Maestro.Core.Dtos.Messages;
using Maestro.Core.Infrastructure;
using Maestro.Core.Model;
using MediatR;

namespace Maestro.Core.Handlers;

public record FlightUpdatedNotification(
    string Callsign,
    string AircraftType,
    WakeCategory WakeCategory,
    string Origin,
    string Destination,
    string? AssignedRunway,
    string? AssignedArrival,
    bool Activated,
    FlightPosition? Position,
    FixDto[] Estimates)
    : INotification;

public class FlightUpdatedHandler(ISequenceProvider sequenceProvider, IMediator mediator, IClock clock)
    : INotificationHandler<FlightUpdatedNotification>
{
    public async Task Handle(FlightUpdatedNotification notification, CancellationToken cancellationToken)
    {
        var sequence = sequenceProvider.TryGetSequence(notification.Destination);
        if (sequence is null)
            return;
        
        var flight = await sequence.TryGetFlight(notification.Callsign, cancellationToken);

        if (flight is null)
        {
            // TODO: Make configurable
            var flightCreationThreshold = TimeSpan.FromHours(2);
            
            var feederFix = notification.Estimates.LastOrDefault(x => sequence.FeederFixes.Contains(x.Identifier));
            var landingEstimate = notification.Estimates.Last().Estimate;
            
            // TODO: Verify if this behaviour is correct
            // Flights not planned via a feeder fix are added to the pending list
            if (feederFix is null)
            {
                flight = CreateMaestroFlight(notification, null, landingEstimate);
                
                // TODO: Revisit flight plan activation
                flight.Activate(clock);
                
                await sequence.AddPending(flight, cancellationToken);
            }
            // Only create flights in Maestro when they're within a specified range of the feeder fix
            else if (feederFix.Estimate - clock.UtcNow() <= flightCreationThreshold)
            {
                flight = CreateMaestroFlight(
                    notification,
                    feederFix,
                    landingEstimate);
                
                // TODO: Revisit flight plan activation
                flight.Activate(clock);
                
                await sequence.Add(flight, cancellationToken);
            }
        }

        if (flight is null)
        {
            return;
        }

        // TODO: Revisit flight plan activation
        // The flight becomes 'active' in Maestro when the flight is activated in TAAATS.
        // It is then updated by regular reports from the TAAATS FDP to the Maestro System.
        // if (!flight.Activated && notification.Activated)
        // {
        //     flight.Activate(clock);
        // }

        if (flight is { Activated: true })
        {
            if (notification.Position.HasValue)
            {
                await mediator.Publish(
                    new FlightPositionReport(
                        flight.Callsign,
                        flight.DestinationIdentifier,
                        notification.Position.Value,
                        notification.Estimates),
                    cancellationToken);
            }
            
            SetState(flight);
            
            // TODO: Should this be limited to unstable?
            // TODO: Calculate ETA_FF
            // TODO: Sequence
            // TODO: Schedule
            // TODO: Calculate STA and STA_FF
            // TODO: Optimise
        }
        
        await mediator.Publish(new SequenceModifiedNotification(sequence.ToDto()), cancellationToken);
    }

    void SetState(Flight flight)
    {
        // TODO: Make configurable
        var stableThreshold = TimeSpan.FromMinutes(25);
        var frozenThreshold = TimeSpan.FromMinutes(15);
        var minUnstableTime = TimeSpan.FromSeconds(180);

        var timeActive = clock.UtcNow() - flight.ActivatedTime;
        var timeToFeeder = flight.EstimatedFeederFixTime - clock.UtcNow();
        var timeToLanding = flight.EstimatedLandingTime - clock.UtcNow();

        switch (flight.State)
        {
            case State.Unstable when timeToFeeder <= stableThreshold && timeActive > minUnstableTime:
                flight.SetState(State.Stable);
                break;
        
            case State.Stable when flight.InitialFeederFixTime < clock.UtcNow():
                flight.SetState(State.SuperStable);
                break;
        
            case State.SuperStable when timeToLanding <= frozenThreshold:
                flight.SetState(State.Frozen);
                break;
        
            case State.Frozen when flight.ScheduledLandingTime < clock.UtcNow():
                flight.SetState(State.Landed);
                break;
        }
    }

    Flight CreateMaestroFlight(
        FlightUpdatedNotification notification,
        FixDto? feederFixEstimate,
        DateTimeOffset landingEstimate)
    {
        return new Flight
        {
            Callsign = notification.Callsign,
            AircraftType = notification.AircraftType,
            WakeCategory = notification.WakeCategory,
            OriginIdentifier = notification.Origin,
            DestinationIdentifier = notification.Destination,
            AssignedRunwayIdentifier = notification.AssignedRunway,
            AssignedStarIdentifier = notification.AssignedArrival,
            HighPriority = feederFixEstimate is null,
            
            FeederFixIdentifier = feederFixEstimate?.Identifier,
            InitialFeederFixTime = feederFixEstimate?.Estimate,
            EstimatedFeederFixTime = feederFixEstimate?.Estimate,
            ScheduledFeederFixTime = feederFixEstimate?.Estimate,
            
            InitialLandingTime = landingEstimate,
            EstimatedLandingTime = landingEstimate,
            ScheduledLandingTime = landingEstimate
        };
    }
}