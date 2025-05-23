﻿using Maestro.Core.Messages;
using Maestro.Core.Model;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Maestro.Core.Handlers;

public class RemoveRequestHandler(ISequenceProvider sequenceProvider, ILogger<RecomputeRequestHandler> logger)
    : IRequestHandler<RemoveRequest, RemoveResponse>
{
    public async Task<RemoveResponse> Handle(RemoveRequest request, CancellationToken cancellationToken)
    {
        var sequence = sequenceProvider.TryGetSequence(request.AirportIdentifier);
        if (sequence == null)
        {
            logger.LogWarning("Sequence not found for airport {AirportIdentifier}.", request.AirportIdentifier);
            return new RemoveResponse();
        }
        
        var flight = await sequence.TryGetFlight(request.Callsign, cancellationToken);
        if (flight == null)
        {
            logger.LogWarning("Flight {Callsign} not found for airport {AirportIdentifier}.", request.Callsign, request.AirportIdentifier);
            return new RemoveResponse();
        }

        throw new NotImplementedException("Remove not yet implemented.");
    }
}