﻿using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Autofac;
using BranchComparer.Infrastructure;
using BranchComparer.Infrastructure.ViewModels;
using BranchComparer.ViewModels;
using NLog;
using NLog.Config;
using NLog.Targets;
using PS.IoC;
using PS.MVVM.Services.WindowService;
using PS.WPF;

namespace BranchComparer;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App
{
    public static ImageSource GetApplicationIcon()
    {
        var iconDecoder = new IconBitmapDecoder(
            new Uri(@"pack://application:,,/icon.ico", UriKind.RelativeOrAbsolute),
            BitmapCreateOptions.None,
            BitmapCacheOption.Default);
        var frame = iconDecoder.Frames.FirstOrDefault(f => f.Width <= SystemParameters.IconWidth) ?? iconDecoder.Frames[0];
        return frame;
    }

    public static string GetApplicationName()
    {
        var assembly = Assembly.GetExecutingAssembly();
        var title = assembly.GetCustomAttribute<AssemblyProductAttribute>()?.Product;

        return title;
    }

    public static string GetApplicationTitle()
    {
        var title = GetApplicationName();
        var version = GetApplicationVersion();
        if (!string.IsNullOrWhiteSpace(version))
        {
            title += ": " + version;
        }

        if (Runtime.IsDebugBuild) title += " (DEBUG)";
        return title;
    }

    public static string GetApplicationVersion()
    {
        var assembly = Assembly.GetExecutingAssembly();
        if (assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>() is AssemblyInformationalVersionAttribute assemblyInformationalVersionAttribute)
        {
            return assemblyInformationalVersionAttribute.InformationalVersion;
        }

        if (assembly.GetCustomAttribute<AssemblyVersionAttribute>() is AssemblyVersionAttribute assemblyVersionAttribute)
        {
            return assemblyVersionAttribute.Version;
        }

        if (assembly.GetCustomAttribute<AssemblyFileVersionAttribute>() is AssemblyFileVersionAttribute assemblyFileVersionAttribute)
        {
            return assemblyFileVersionAttribute.Version;
        }

        return string.Empty;
    }

    private static void ConfigureLogger()
    {
        var workingDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
        if (!Directory.Exists(workingDirectory)) return;

        var nlogConfigurationFilePath = Path.Combine(workingDirectory, "Nlog.config");
        if (File.Exists(nlogConfigurationFilePath))
        {
            try
            {
                LogManager.LoadConfiguration(nlogConfigurationFilePath);
            }
            catch (Exception e)
            {
                if (Debugger.IsAttached)
                {
                    Debug.WriteLine($"Logger configuration cannot be loaded. Details: {e.GetBaseException().Message}");
                }
            }
        }

        if (LogManager.Configuration == null)
        {
            var config = new LoggingConfiguration();
            var debuggerTarget = new DebuggerTarget("target")
            {
                Layout = "${time} ${uppercase:${level}}(${callsite}) - ${message} ${exception:format=tostring}",
            };
            config.AddTarget(debuggerTarget);
            config.AddRuleForAllLevels(debuggerTarget); // all to debugger
            LogManager.Configuration = config;
        }
    }

    private readonly Bootstrapper _bootstrapper;
    private readonly ILogger _logger;

    static App()
    {
        ConfigureLogger();
    }

    public App()
    {
        try
        {
            _logger = LogManager.GetCurrentClassLogger();
            _logger.Trace("Application logger was created successfully.");
        }
        catch (Exception e)
        {
            var message = "Unrecoverable error on logger creation";
            if (Debugger.IsAttached)
            {
                Debug.WriteLine($"{message}. Details: {e.GetBaseException().Message}");
            }

            FatalShutdown(e, message);
            return;
        }

        try
        {
            var bootstrapperLogger = new RelayBootstrapperLogger
            {
                TraceAction = message => _logger.Trace(message),
                DebugAction = message => _logger.Debug(message),
                InfoAction = message => _logger.Info(message),
                WarnAction = message => _logger.Warn(message),
                ErrorAction = message => _logger.Error(message),
                FatalAction = message => _logger.Fatal(message),
            };

            _bootstrapper = new Bootstrapper(bootstrapperLogger);
        }
        catch (Exception e)
        {
            FatalShutdown(e, "Bootstrapper cannot be created");
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        base.OnExit(e);

        if (_bootstrapper == null) return;

        try
        {
            _bootstrapper.Dispose();
        }
        catch
        {
            //Nothing 
        }
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        var startupInitialTime = DateTime.Now;

        AppDomain.CurrentDomain.UnhandledException += CurrentDomainOnUnhandledException;
        DispatcherUnhandledException += DispatcherOnUnhandledException;

        _logger.Info("--------------------------------");
        DumpApplicationInformation();

        try
        {
            InitializeBootstrapper();

            base.OnStartup(e);

            ShowShell();

            _logger.Info("{0} started", GetApplicationTitle());
            _logger.Debug("Startup time: {0:g}", DateTime.Now - startupInitialTime);
        }
        catch (Exception exception)
        {
            FatalShutdown(exception, "Application failed");
        }
    }

    private void CurrentDomainOnUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        var exception = e.ExceptionObject as Exception;
        var message = "Unrecoverable unhandled exception has occurred";

        _logger.Fatal(new ApplicationException(message, exception));
        _bootstrapper.Dispose();
    }

    private async void DispatcherOnUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        e.Handled = true;

        //Catch all NotificationException and display notification dialog.
        if (e.Exception is NotificationException notificationException)
        {
            try
            {
                var message = $"{e.Exception.Message}.";
                if (e.Exception.InnerException != null)
                {
                    message += " " + $"Details: {e.Exception.GetBaseException().Message}";
                }

                await _bootstrapper.Container
                                   .Resolve<IWindowService>()
                                   .ShowModalAsync(new NotificationViewModel
                                   {
                                       Title = notificationException.Title ?? "Error",
                                       Content = message,
                                   });
            }
            catch (Exception exception)
            {
                _logger.Error(new ApplicationException("Notification dialog can not be displayed", exception));
            }
        }
        else
        {
            _logger.Error(new ApplicationException("Recoverable unhandled exception has occurred", e.Exception));
        }
    }

    private void DumpApplicationInformation()
    {
        var currentAssembly = Assembly.GetEntryAssembly();
        if (currentAssembly != null)
        {
            var version = currentAssembly.GetCustomAttribute<AssemblyFileVersionAttribute>()?.Version;
            var informationalVersion = currentAssembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
            var framework = currentAssembly.GetCustomAttribute<TargetFrameworkAttribute>()?.FrameworkName;
            var architecture = RuntimeInformation.ProcessArchitecture;
            var buildMode = Runtime.IsDebugBuild ? "Debug" : "Release";
            var debuggingPanelState = "Disabled";
            if (Runtime.IsDebugMode)
            {
                debuggingPanelState = "Active";
                if (Runtime.IsDebugBuild)
                {
                    debuggingPanelState += " " + "(Debug build)";
                }
                else if (Runtime.IsDebugModeForced)
                {
                    debuggingPanelState += " " + "(Forced via command line)";
                }
            }

            _logger.Debug("Application debug information");
            _logger.Debug("* Framework: {0}", framework);
            _logger.Debug("* Process architecture: {0}", architecture);
            _logger.Debug("* File version: {0}", version ?? "<not specified>");
            _logger.Debug("* Informational version: {0}", informationalVersion ?? "<not specified>");
            _logger.Debug("* Build: {0}", buildMode);
            _logger.Debug("* Debug panel: {0}", debuggingPanelState);
        }
    }

    private void FatalShutdown(Exception e, string message)
    {
        _logger?.Fatal(e, message);
        Shutdown(-1);

        MessageBox.Show($"{message}. Details: {e.GetBaseException().Message}",
                        "Fatal error",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
    }

    private void InitializeBootstrapper()
    {
        try
        {
            _bootstrapper.Initialize();
        }
        catch (Exception exception)
        {
            throw new ApplicationException("Bootstrapper initialization failed", exception);
        }
    }

    private void ShowShell()
    {
        try
        {
            _logger.Trace("Showing shell...");
            _bootstrapper.Container.Resolve<IWindowService>().Show<ShellViewModel>();
            _logger.Debug("Shell shown successfully");
        }
        catch (Exception exception)
        {
            throw new ApplicationException("Shell initialization failed", exception);
        }
    }
}
