﻿using BranchComparer.Infrastructure.Services;
using BranchComparer.Infrastructure.Services.EnvironmentService;
using Newtonsoft.Json;
using PS;
using PS.IoC.Attributes;
using PS.MVVM.Patterns;

namespace BranchComparer.ViewModels;

[DependencyRegisterAsSelf]
[JsonObject(MemberSerialization.OptIn)]
public class FilterViewModel : BaseNotifyPropertyChanged,
                               IViewModel
{
    private bool _isExpanded;

    public FilterViewModel(ISettingsService settingsService, IEnvironmentService environmentService)
    {
        EnvironmentService = environmentService;

        _isExpanded = true;

        settingsService.LoadPopulateAndSaveOnDispose(GetType().AssemblyQualifiedName, this);
    }

    public IEnvironmentService EnvironmentService { get; }

    [JsonProperty]
    public bool IsExpanded
    {
        get { return _isExpanded; }
        set { SetField(ref _isExpanded, value); }
    }
}
