﻿using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Maestro.Core.Dtos;
using Maestro.Core.Dtos.Configuration;
using Maestro.Core.Dtos.Messages;
using Maestro.Core.Handlers;
using MediatR;

namespace Maestro.Wpf.ViewModels;

public partial class MaestroViewModel : ObservableObject
{
    [ObservableProperty]
    ObservableCollection<AirportViewModel> _availableAirports = [];

    [ObservableProperty]
    AirportViewModel? _selectedAirport;

    [ObservableProperty]
    RunwayModeViewModel? _selectedRunwayMode;

    [ObservableProperty]
    ViewConfigurationDto? _selectedView;

    readonly IMediator _mediator;

    [ObservableProperty]
    List<FlightViewModel> _aircraft = [];

    public MaestroViewModel(IMediator mediator)
    {
        _mediator = mediator;
    }

    partial void OnAvailableAirportsChanged(ObservableCollection<AirportViewModel> availableAirports)
    {
        // Select the first airport if the selected one no longer exists
        if (SelectedAirport == null || !availableAirports.Any(a => a.Identifier == SelectedAirport.Identifier))
        {
            SelectedAirport = availableAirports.FirstOrDefault();
        }
    }

    partial void OnSelectedAirportChanged(AirportViewModel? airportViewModel)
    {
        if (airportViewModel is null)
        {
            SelectedRunwayMode = null;
            SelectedView = null;
            return;
        }

        if (SelectedRunwayMode == null || airportViewModel.RunwayModes.All(r => r.Identifier != SelectedRunwayMode.Identifier))
        {
            SelectedRunwayMode = airportViewModel.RunwayModes.FirstOrDefault();
        }

        if (SelectedView == null || airportViewModel.Views.All(s => s.Identifier != SelectedView.Identifier))
        {
            SelectedView = airportViewModel.Views.FirstOrDefault();
        }

        var response = _mediator.Send(new GetSequenceRequest(SelectedAirport.Identifier)).GetAwaiter().GetResult();
        RefreshSequence(response.Sequence);
    }

    [RelayCommand]
    async Task LoadConfiguration()
    {
        var response = await _mediator.Send(new GetAirportConfigurationRequest(), CancellationToken.None);

        AvailableAirports.Clear();

        foreach (var airport in response.Airports)
        {
            var runwayModes = airport.RunwayModes.Select(rm =>
                new RunwayModeViewModel(
                    rm.Identifier,
                    rm.Runways.Select(r =>
                        new RunwayViewModel(r.Identifier, TimeSpan.FromSeconds(r.DefaultLandingRateSeconds)))
                    .ToArray()))
                .ToArray();

            AvailableAirports.Add(new AirportViewModel(airport.Identifier, runwayModes, airport.Views));
        }
    }

    [RelayCommand]
    void SelectView(ViewConfigurationDto? viewConfiguration)
    {
        SelectedView = viewConfiguration;
    }

    public void RefreshSequence(SequenceDto sequence)
    {
        Aircraft = sequence.Flights.Select(a =>
                new FlightViewModel(
                    a.Callsign,
                    a.AircraftType,
                    a.WakeCategory,
                    a.Origin,
                    a.Destination,
                    a.State,
                    -1, // TODO:
                    a.FeederFix,
                    a.InitialFeederFixTime,
                    a.EstimatedFeederFixTime,
                    a.ScheduledFeederFixTime,
                    a.AssignedRunway,
                    -1, // TODO:
                    a.InitialLandingTime,
                    a.EstimatedLandingTime,
                    a.ScheduledLandingTime,
                    a.InitialDelay,
                    a.CurrentDelay))
            .ToList();
    }

    [RelayCommand]
    async Task ShowInformationWindow(FlightViewModel viewModel)
    {
        var request = new OpenInformationWindowRequest(viewModel);

        await _mediator.Send(request);
    }
}
