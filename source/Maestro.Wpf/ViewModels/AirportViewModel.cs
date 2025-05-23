﻿using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Maestro.Core.Configuration;
using Maestro.Core.Model;

namespace Maestro.Wpf.ViewModels;

public partial class AirportViewModel(string identifier, RunwayModeViewModel[] runwayModes, ViewConfiguration[] views) : ObservableObject
{
    public string Identifier => identifier;

    [ObservableProperty]
    ObservableCollection<RunwayModeViewModel> _runwayModes = new(runwayModes);

    [ObservableProperty]
    ObservableCollection<ViewConfiguration> _views = new(views);
}

public class RunwayModeViewModel(string identifier, RunwayViewModel[] runways)
{
    public string Identifier => identifier;

    public RunwayViewModel[] Runways => runways;
}

public partial class RunwayViewModel(string identifier, TimeSpan defaultLandingRate) : ObservableObject
{
    [ObservableProperty]
    TimeSpan _landingRate = defaultLandingRate;
    
    public string Identifier { get; } = identifier;

    public TimeSpan DefaultLandingRate { get; } = defaultLandingRate;
}
