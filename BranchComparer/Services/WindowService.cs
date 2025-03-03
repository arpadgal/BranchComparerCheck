﻿using System.Windows;
using Autofac;
using PS.IoC.Attributes;
using PS.MVVM.Services;
using PS.MVVM.Services.WindowService;
using PS.WPF.Controls;
using Window = System.Windows.Window;

namespace BranchComparer.Services;

[DependencyRegisterAsInterface(typeof(IWindowService))]
[DependencyLifetime(DependencyLifetime.InstanceSingle)]
internal class WindowService : PS.MVVM.Services.WindowService.WindowService,
                               IDisposable
{
    private readonly ILifetimeScope _scope;
    private readonly IViewResolverService _viewResolverService;

    public WindowService(IViewResolverService viewResolverService,
                         ILifetimeScope scope)
    {
        _viewResolverService = viewResolverService;
        _scope = scope;
    }

    protected override Window CreateWindow()
    {
        var result = new ChromelessWindow
        {
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
        };

        return result;
    }

    protected override IViewAssociation GetAssociation(Type consumerServiceType, Type viewModelType, string key)
    {
        return _viewResolverService.Find(consumerServiceType, viewModelType, key);
    }

    protected override void OnPreviewWindowShow<TViewModel>(Window window, TViewModel viewModel, string region)
    {
    }

    protected override void OnWindowClose<TViewModel>(Window window, TViewModel viewModel, string region)
    {
    }

    protected override object Resolve(Type type)
    {
        return _scope.Resolve(type);
    }

    public void Dispose()
    {
    }
}
